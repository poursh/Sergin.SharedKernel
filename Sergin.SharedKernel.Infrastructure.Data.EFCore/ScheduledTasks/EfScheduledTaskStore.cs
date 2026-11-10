using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Sergin.SharedKernel.Application.ScheduledTasks;
using Sergin.SharedKernel.Domain.ScheduledTasks;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.ScheduledTasks;

/// <summary>
/// One opted-in module's <see cref="IScheduledTaskStore"/>, over its own <typeparamref name="TContext"/>; registered
/// keyed by the module's schema by <c>AddModuleDbContext</c>. Tasks are a scoped aggregate, so their reads take the
/// scope filter unless the caller asks to ignore it (the soft-delete filter always applies); runs and run items are
/// not scoped and take no filter.
/// </summary>
internal sealed class EfScheduledTaskStore<TContext>(TContext context, string schema) : IScheduledTaskStore
    where TContext : DbContext, IScheduledTasksDbContext
{
    // Built once. The identifiers are what ApplyScheduledTasks maps in this schema; the run id is a {0} parameter.
    // A prebuilt string, not an interpolated literal, keeps EF1002 quiet.
    private readonly string lockRunSql =
        "SELECT 1 FROM \"" + schema + "\".\"scheduled_task_runs\" WHERE id = {0} FOR UPDATE";

    public string Schema => schema;

    public Task<ScheduledTask?> GetTaskAsync(ScheduledTaskId id, bool ignoreScope, CancellationToken ct)
    {
        IQueryable<ScheduledTask> tasks = ignoreScope
            ? context.ScheduledTasks.IgnoreQueryFilters([ScopeColumns.QueryFilterName])
            : context.ScheduledTasks;

        return tasks.SingleOrDefaultAsync(task => task.Id == id, ct);
    }

    public (Guid ScopeId, string ScopePath) ScopeOf(ScheduledTask task)
    {
        EntityEntry<ScheduledTask> entry = context.Entry(task);

        return (
            (Guid)entry.Property(ScopeColumns.ScopeId).CurrentValue!,
            ((LTree)entry.Property(ScopeColumns.ScopePath).CurrentValue!).ToString());
    }

    public void AddTask(ScheduledTask task) => context.ScheduledTasks.Add(task);

    public void RemoveTask(ScheduledTask task) => context.ScheduledTasks.Remove(task);

    public void AddRun(ScheduledTaskRun run, IReadOnlyCollection<ScheduledTaskRunItem> items)
    {
        context.ScheduledTaskRuns.Add(run);
        context.ScheduledTaskRunItems.AddRange(items);
    }

    public Task<bool> HasUnfinishedRunAsync(ScheduledTaskId id, CancellationToken ct) =>
        context.ScheduledTaskRuns.AnyAsync(run => run.TaskId == id && run.Status == ScheduledRunStatus.Running, ct);

    public async Task<(ScheduledTaskRun Run, ScheduledTaskRunItem Item)?> GetRunItemAsync(Guid runItemId, CancellationToken ct)
    {
        ScheduledTaskRunItem? item = await context.ScheduledTaskRunItems.SingleOrDefaultAsync(one => one.Id == runItemId, ct);

        if (item is null)
        {
            return null;
        }

        ScheduledTaskRun? run = await context.ScheduledTaskRuns.SingleOrDefaultAsync(one => one.Id == item.RunId, ct);

        return run is null ? null : (run, item);
    }

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

        ScheduledTaskRun? run = await context.ScheduledTaskRuns.FindAsync([runId], ct);

        if (run is null)
        {
            return;
        }

        await context.Entry(run).ReloadAsync(ct);

        if (run.Status != ScheduledRunStatus.Running)
        {
            return;
        }

        var counts = await context.ScheduledTaskRunItems
            .Where(item => item.RunId == runId)
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(ct);

        if (counts.Exists(count => count.Status is not (ScheduledRunItemStatus.Succeeded or ScheduledRunItemStatus.Failed)))
        {
            return;
        }

        run.Complete(
            counts.Where(count => count.Status == ScheduledRunItemStatus.Succeeded).Sum(count => count.Count),
            counts.Where(count => count.Status == ScheduledRunItemStatus.Failed).Sum(count => count.Count),
            at);

        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyCollection<ScheduledTaskTriggerState>> ListTriggersAsync(CancellationToken ct)
    {
        // The scope filter is lifted, the soft-delete filter kept: a deleted task's trigger is an orphan.
        var live = await context.ScheduledTasks
            .IgnoreQueryFilters([ScopeColumns.QueryFilterName])
            .AsNoTracking()
            .Select(task => new { task.Id, task.EngineTriggerId, task.Cron, task.TimeZoneId, task.State })
            .ToListAsync(ct);

        return [.. live.Select(task => new ScheduledTaskTriggerState(
            task.Id,
            task.EngineTriggerId,
            new ScheduledTrigger(schema, task.Id, task.Cron, task.TimeZoneId),
            task.State == ScheduledTaskState.Active))];
    }

    public async Task<bool> TryClaimRunItemAsync(Guid runItemId, DateTime now, DateTime staleBefore, CancellationToken ct) =>
        await context.ScheduledTaskRunItems
            .Where(item => item.Id == runItemId)
            .Where(item => item.Status == ScheduledRunItemStatus.Pending
                || item.Status == ScheduledRunItemStatus.Retrying
                || item.Status == ScheduledRunItemStatus.Running && item.LastActivityAtUtc < staleBefore)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(item => item.Status, ScheduledRunItemStatus.Running)
                    .SetProperty(item => item.Attempts, item => item.Attempts + 1)
                    .SetProperty(item => item.StartedAtUtc, item => item.StartedAtUtc ?? now)
                    .SetProperty(item => item.LastActivityAtUtc, now),
                ct) == 1;

    public async Task<IReadOnlyCollection<Guid>> ListStaleRunItemsAsync(DateTime quietSince, CancellationToken ct) =>
        await Unfinished(quietSince).Select(item => item.Id).ToListAsync(ct);

    public async Task<bool> RelinkStaleRunItemAsync(Guid runItemId, Guid engineJobId, DateTime now, DateTime quietSince, CancellationToken ct) =>
        await Unfinished(quietSince)
            .Where(item => item.Id == runItemId)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(item => item.EngineJobId, engineJobId)
                    .SetProperty(item => item.LastActivityAtUtc, now)
                    .SetProperty(
                        item => item.Status,
                        item => item.Status == ScheduledRunItemStatus.Running ? ScheduledRunItemStatus.Retrying : item.Status),
                ct) == 1;

    private IQueryable<ScheduledTaskRunItem> Unfinished(DateTime quietSince) =>
        context.ScheduledTaskRunItems.Where(item =>
            (item.Status == ScheduledRunItemStatus.Pending
                || item.Status == ScheduledRunItemStatus.Running
                || item.Status == ScheduledRunItemStatus.Retrying)
            && item.LastActivityAtUtc < quietSince);

    /// <summary>Finished runs only: a run still in progress is kept whatever its age. Its items go by the FK's cascade.</summary>
    public Task<int> PurgeAsync(DateTime olderThanUtc, CancellationToken ct) =>
        context.ScheduledTaskRuns
            .Where(run => run.CompletedAtUtc != null && run.CompletedAtUtc < olderThanUtc)
            .ExecuteDeleteAsync(ct);

    public async Task<bool> TrySaveStartedRunAsync(CancellationToken ct)
    {
        try
        {
            await context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is Npgsql.PostgresException { SqlState: "23505", ConstraintName: ScheduledTasksModelBuilderExtensions.OneRunningIndexName })
        {
            context.ChangeTracker.Clear();
            return false;
        }
    }

    public Task SaveAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
