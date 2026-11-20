using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Sergin.SharedKernel.Application.Tasks;
using Sergin.SharedKernel.Domain.Tasks;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Tasks;

/// <summary>
/// One opted-in module's <see cref="ITaskStore"/>, over its own <typeparamref name="TContext"/>; registered
/// keyed by the module's schema by <c>AddModuleDbContext</c>. Tasks are a scoped aggregate, so their reads take the
/// scope filter unless the caller asks to ignore it (the soft-delete filter always applies); runs and run items are
/// not scoped and take no filter.
/// </summary>
internal sealed class EfTaskStore<TContext>(TContext context, string schema) : ITaskStore
    where TContext : DbContext, ITasksDbContext
{
    // Built once. The identifiers are what ApplyTasks maps in this schema; the run id is a {0} parameter.
    // A prebuilt string, not an interpolated literal, keeps EF1002 quiet.
    private readonly string lockRunSql =
        "SELECT 1 FROM \"" + schema + "\".\"task_runs\" WHERE id = {0} FOR UPDATE";

    // The heartbeat in one round trip: the stamp of a Running item, returning whether its run was asked to cancel. The item
    // id is {0}, the time {1}; a single "Value" column is what SqlQueryRaw<bool> reads.
    private readonly string touchRunItemSql =
        "UPDATE \"" + schema + "\".\"task_run_items\" AS item SET last_activity_at_utc = {1} "
        + "WHERE item.id = {0} AND item.status = 'Running' "
        + "RETURNING (SELECT run.cancel_requested_at_utc IS NOT NULL FROM \"" + schema + "\".\"task_runs\" AS run "
        + "WHERE run.id = item.run_id) AS \"Value\"";

    public string Schema => schema;

    public Task<TaskSchedule?> GetTaskAsync(TaskScheduleId id, bool ignoreScope, CancellationToken ct)
    {
        IQueryable<TaskSchedule> tasks = ignoreScope
            ? context.TaskSchedules.IgnoreQueryFilters([ScopeColumns.QueryFilterName])
            : context.TaskSchedules;

        return tasks.SingleOrDefaultAsync(task => task.Id == id, ct);
    }

    public (Guid ScopeId, string ScopePath) ScopeOf(TaskSchedule task)
    {
        EntityEntry<TaskSchedule> entry = context.Entry(task);

        return (
            (Guid)entry.Property(ScopeColumns.ScopeId).CurrentValue!,
            ((LTree)entry.Property(ScopeColumns.ScopePath).CurrentValue!).ToString());
    }

    public void AddTask(TaskSchedule task) => context.TaskSchedules.Add(task);

    public void RemoveTask(TaskSchedule task) => context.TaskSchedules.Remove(task);

    public void AddRun(TaskRun run, IReadOnlyCollection<TaskRunItem> items)
    {
        context.TaskRuns.Add(run);
        context.TaskRunItems.AddRange(items);
    }

    public Task<bool> HasUnfinishedRunAsync(TaskScheduleId id, CancellationToken ct) =>
        context.TaskRuns.AnyAsync(run => run.ScheduleId == id && run.Status == TaskRunStatus.Running, ct);

    public Task<bool> HasRunningCollectionRunAsync(string commandName, Guid scopeId, CancellationToken ct) =>
        context.TaskRuns.AnyAsync(
            run => run.CommandName == commandName
                && run.ScopeId == scopeId
                && run.Status == TaskRunStatus.Running
                && run.TargetKind == TaskRunTargetKind.Collection,
            ct);

    public async Task<(TaskRun Run, TaskRunItem Item)?> GetRunItemAsync(Guid runItemId, CancellationToken ct)
    {
        TaskRunItem? item = await context.TaskRunItems.SingleOrDefaultAsync(one => one.Id == runItemId, ct);

        if (item is null)
        {
            return null;
        }

        TaskRun? run = await context.TaskRuns.SingleOrDefaultAsync(one => one.Id == item.RunId, ct);

        return run is null ? null : (run, item);
    }

    public Task<TaskRun?> GetRunAsync(Guid runId, CancellationToken ct) =>
        context.TaskRuns.SingleOrDefaultAsync(run => run.Id == runId, ct);

    /// <summary>
    /// Under a lock on the run's row, so items finishing in parallel cannot each see the other unfinished and leave
    /// the run open: whichever takes the lock last counts every item already settled. The run is reloaded under the
    /// lock, so a run another call completed is left alone.
    /// </summary>
    public async Task CompleteRunIfFinishedAsync(Guid runId, DateTime at, CancellationToken ct)
    {
        // Every early return below leaves the transaction uncommitted: disposing it rolls back and releases the lock.
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(ct);
        await context.Database.ExecuteSqlRawAsync(lockRunSql, [runId], ct);

        TaskRun? run = await context.TaskRuns.FindAsync([runId], ct);

        if (run is null)
        {
            return;
        }

        await context.Entry(run).ReloadAsync(ct);

        if (run.Status != TaskRunStatus.Running)
        {
            return;
        }

        var counts = await context.TaskRunItems
            .Where(item => item.RunId == runId)
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(ct);

        if (counts.Exists(count => count.Status is not (TaskRunItemStatus.Succeeded or TaskRunItemStatus.Failed or TaskRunItemStatus.Cancelled)))
        {
            return;
        }

        run.Complete(
            counts.Where(count => count.Status == TaskRunItemStatus.Succeeded).Sum(count => count.Count),
            counts.Where(count => count.Status == TaskRunItemStatus.Failed).Sum(count => count.Count),
            counts.Where(count => count.Status == TaskRunItemStatus.Cancelled).Sum(count => count.Count),
            at);

        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>
    /// Under the run's row lock, like <see cref="CompleteRunIfFinishedAsync"/>: a completion waits for the cancel and then
    /// counts the cancelled items, and a cancel after a completion finds the run no longer Running. Both writes are
    /// set-based, not through the change tracker, so a run the caller holds tracked is never saved over; the completion
    /// that follows reloads it.
    /// </summary>
    public async Task CancelRunAsync(Guid runId, DateTime at, CancellationToken ct)
    {
        // An early return leaves the transaction uncommitted: disposing it rolls back and releases the lock.
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(ct);
        await context.Database.ExecuteSqlRawAsync(lockRunSql, [runId], ct);

        // The first request's time is kept: a second cancel changes nothing on the run.
        int running = await context.TaskRuns
            .Where(run => run.Id == runId && run.Status == TaskRunStatus.Running)
            .ExecuteUpdateAsync(set => set.SetProperty(run => run.CancelRequestedAtUtc, run => run.CancelRequestedAtUtc ?? at), ct);

        if (running == 0)
        {
            return;
        }

        // Only items no delivery holds: a Running item is its delivery's to stop.
        await context.TaskRunItems
            .Where(item => item.RunId == runId
                && (item.Status == TaskRunItemStatus.Pending || item.Status == TaskRunItemStatus.Retrying))
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(item => item.Status, TaskRunItemStatus.Cancelled)
                    .SetProperty(item => item.CompletedAtUtc, at),
                ct);

        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyCollection<TaskScheduleTriggerState>> ListTriggersAsync(CancellationToken ct)
    {
        // The scope filter is lifted, the soft-delete filter kept: a deleted task's trigger is an orphan.
        var live = await context.TaskSchedules
            .IgnoreQueryFilters([ScopeColumns.QueryFilterName])
            .AsNoTracking()
            .Select(task => new { task.Id, task.EngineTriggerId, task.Cron, task.TimeZoneId, task.State })
            .ToListAsync(ct);

        return [.. live.Select(task => new TaskScheduleTriggerState(
            task.Id,
            task.EngineTriggerId,
            new TaskScheduleTrigger(schema, task.Id, task.Cron, task.TimeZoneId),
            task.State == TaskScheduleState.Active))];
    }

    public async Task<bool> TryClaimRunItemAsync(Guid runItemId, DateTime now, DateTime staleBefore, CancellationToken ct) =>
        await context.TaskRunItems
            .Where(item => item.Id == runItemId)
            // A run asked to cancel starts nothing more, a stale Running item's re-delivery included.
            .Where(item => context.TaskRuns.Any(run => run.Id == item.RunId && run.CancelRequestedAtUtc == null))
            .Where(item => item.Status == TaskRunItemStatus.Pending
                || item.Status == TaskRunItemStatus.Retrying
                || item.Status == TaskRunItemStatus.Running && item.LastActivityAtUtc < staleBefore)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(item => item.Status, TaskRunItemStatus.Running)
                    .SetProperty(item => item.Attempts, item => item.Attempts + 1)
                    .SetProperty(item => item.StartedAtUtc, item => item.StartedAtUtc ?? now)
                    .SetProperty(item => item.LastActivityAtUtc, now)
                    // A new attempt starts from nothing. It also means the executor, which reads the item after the claim,
                    // holds it with no progress, so the last value it puts on the item before the outcome's save is always
                    // a change the save writes, whatever the throttled writes left in the row meanwhile.
                    .SetProperty(item => item.Progress, (int?)null),
                ct) == 1;

    /// <summary>
    /// Set-based, so the item the executor holds tracked is never saved over; the executor puts the last value on that
    /// item itself before the outcome's save.
    /// </summary>
    public async Task SetRunItemProgressAsync(Guid runItemId, int percent, DateTime now, CancellationToken ct) =>
        await context.TaskRunItems
            .Where(item => item.Id == runItemId && item.Status == TaskRunItemStatus.Running)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(item => item.Progress, Math.Clamp(percent, 0, TaskRunItem.MaxProgress))
                    .SetProperty(item => item.LastActivityAtUtc, now),
                ct);

    /// <summary>
    /// The stamp and the read of the run's cancel request in one statement, set-based, so an item the caller holds tracked
    /// is never saved over. An item no longer Running is not stamped, and its run's cancel request is then read on its own.
    /// A cancel request is never withdrawn, so a read that sees none only means a later beat will.
    /// </summary>
    public async Task<RunItemHeartbeat> TouchRunItemAsync(Guid runItemId, DateTime now, CancellationToken ct)
    {
        // Not composed further (no First or Single), so EF sends the UPDATE as written rather than wrapping it.
        List<bool> touched = await context.Database.SqlQueryRaw<bool>(touchRunItemSql, runItemId, now).ToListAsync(ct);

        if (touched.Count == 1)
        {
            return new RunItemHeartbeat(true, touched[0]);
        }

        bool cancelRequested = await context.TaskRunItems
            .Where(item => item.Id == runItemId)
            .AnyAsync(item => context.TaskRuns.Any(run => run.Id == item.RunId && run.CancelRequestedAtUtc != null), ct);

        return new RunItemHeartbeat(false, cancelRequested);
    }

    /// <summary>
    /// Set-based, like <see cref="CancelRunAsync"/>. The runs are read first and the update repeats the whole condition,
    /// so an item that finished in between is left alone; its run is answered anyway, and completing it is idempotent.
    /// </summary>
    public async Task<IReadOnlyCollection<Guid>> CancelAbandonedRunItemsAsync(DateTime quietSince, DateTime at, CancellationToken ct)
    {
        IQueryable<TaskRunItem> abandoned = UnfinishedQuiet(quietSince)
            .Where(item => context.TaskRuns.Any(run => run.Id == item.RunId && run.CancelRequestedAtUtc != null));

        List<Guid> runs = await abandoned.Select(item => item.RunId).Distinct().ToListAsync(ct);

        if (runs.Count == 0)
        {
            return [];
        }

        await abandoned.ExecuteUpdateAsync(
            set => set
                .SetProperty(item => item.Status, TaskRunItemStatus.Cancelled)
                .SetProperty(item => item.CompletedAtUtc, at),
            ct);

        return runs;
    }

    public async Task<IReadOnlyCollection<Guid>> ListStaleRunItemsAsync(DateTime quietSince, CancellationToken ct) =>
        await Unfinished(quietSince).Select(item => item.Id).ToListAsync(ct);

    public async Task<IReadOnlyCollection<Guid>> ListRunsToCompleteAsync(DateTime quietSinceUtc, CancellationToken ct) =>
        await context.TaskRuns
            .Where(run => run.Status == TaskRunStatus.Running && run.StartedAtUtc < quietSinceUtc)
            .Where(run => !context.TaskRunItems.Any(item =>
                item.RunId == run.Id
                && (item.Status == TaskRunItemStatus.Pending
                    || item.Status == TaskRunItemStatus.Running
                    || item.Status == TaskRunItemStatus.Retrying)))
            .Select(run => run.Id)
            .ToListAsync(ct);

    public async Task<bool> RelinkStaleRunItemAsync(Guid runItemId, Guid engineJobId, DateTime now, DateTime quietSince, CancellationToken ct) =>
        await Unfinished(quietSince)
            .Where(item => item.Id == runItemId)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(item => item.EngineJobId, engineJobId)
                    .SetProperty(item => item.LastActivityAtUtc, now)
                    .SetProperty(
                        item => item.Status,
                        item => item.Status == TaskRunItemStatus.Running ? TaskRunItemStatus.Retrying : item.Status),
                ct) == 1;

    // An item of a run asked to cancel is never handed to the engine again: CancelAbandonedRunItemsAsync settles it.
    private IQueryable<TaskRunItem> Unfinished(DateTime quietSince) =>
        UnfinishedQuiet(quietSince)
            .Where(item => context.TaskRuns.Any(run => run.Id == item.RunId && run.CancelRequestedAtUtc == null));

    private IQueryable<TaskRunItem> UnfinishedQuiet(DateTime quietSince) =>
        context.TaskRunItems.Where(item =>
            (item.Status == TaskRunItemStatus.Pending
                || item.Status == TaskRunItemStatus.Running
                || item.Status == TaskRunItemStatus.Retrying)
            && item.LastActivityAtUtc < quietSince);

    /// <summary>Finished runs only: a run still in progress is kept whatever its age. Its items go by the FK's cascade.</summary>
    public Task<int> PurgeAsync(DateTime olderThanUtc, CancellationToken ct) =>
        context.TaskRuns
            .Where(run => run.CompletedAtUtc != null && run.CompletedAtUtc < olderThanUtc)
            .ExecuteDeleteAsync(ct);

    public async Task<StartedRunSave> TrySaveStartedRunAsync(CancellationToken ct)
    {
        try
        {
            await context.SaveChangesAsync(ct);
            return StartedRunSave.Saved;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is Npgsql.PostgresException { SqlState: "23505", ConstraintName: TasksModelBuilderExtensions.OneRunningIndexName })
        {
            context.ChangeTracker.Clear();
            return StartedRunSave.ScheduleRunning;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is Npgsql.PostgresException { SqlState: "23505", ConstraintName: TasksModelBuilderExtensions.CollectionRunningIndexName })
        {
            context.ChangeTracker.Clear();
            return StartedRunSave.CollectionRunning;
        }
    }

    public Task SaveAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
