using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Application.ScheduledTasks;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Domain.ScheduledTasks;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.ScheduledTasks;

namespace Sergin.SharedKernel.Hosts.ScheduledTasks;

/// <summary>
/// Sergin's rows are the definition and the engine's store only their copy, so at host start the copy is put back in
/// step, for every module that opted into scheduled tasks:
/// <list type="bullet">
/// <item>a live task whose trigger the engine does not hold (none linked, or an id the engine lost) gets a new one, and
/// the row links it; a paused task's trigger, new or kept, is disabled;</item>
/// <item>a run item still Pending with no engine job, of a run started more than <see cref="Grace"/> ago, is handed to
/// the engine: its host stopped between saving the run and enqueueing the item. An item stuck Running is not touched
/// here: its engine job is leased, and the engine restarts a job whose node stopped renewing the lease;</item>
/// <item>a trigger the engine holds for one of this host's schemas that no live task links, made more than
/// <see cref="Grace"/> ago, is removed: its task was deleted while the engine was unreachable, or never saved.</item>
/// </list>
/// The grace keeps the rebuild off work another node, or a request on this one, is doing right now: a create schedules
/// its trigger before it saves its row, and a starter saves its run before it enqueues the items.
/// <para>
/// The work runs in the background, after start: an engine may refuse changes until it is activated, which a host does
/// once started. A step that fails is logged and the next goes on; nothing here stops the host. What does stop it is a
/// host that opted a module in but registered no <see cref="IScheduledTaskEngine"/>: every schedule would fail on its
/// first engine call, so start fails instead, naming the schemas.
/// </para>
/// <para>Public, like <c>OutboxRelayService</c>, so an integration test can find its registration and remove it.</para>
/// </summary>
public sealed partial class ScheduledTaskTriggerRebuildService(
    IServiceProvider services,
    IEnumerable<ScheduledTasksRegistration> registrations,
    IDateTimeProvider clock,
    ILogger<ScheduledTaskTriggerRebuildService> logger) : BackgroundService
{
    /// <summary>How old a run or a trigger must be before the rebuild treats it as left behind.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(1);

    private readonly string[] schemas = [.. registrations.Select(registration => registration.Schema).Distinct(StringComparer.Ordinal)];

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (schemas.Length > 0 && !services.GetRequiredService<IServiceProviderIsService>().IsService(typeof(IScheduledTaskEngine)))
        {
            throw new InvalidOperationException(
                $"Scheduled tasks are enabled for schema(s) {string.Join(", ", schemas)}, but no {nameof(IScheduledTaskEngine)} is registered. "
                + "Register the job engine (AddSerginTickerQ) in the host, or remove IScheduledTasksDbContext from the module's DbContext.");
        }

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (schemas.Length == 0)
        {
            return;
        }

        try
        {
            await RebuildAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown before the rebuild finished: the next start does it again.
        }
    }

    private async Task RebuildAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IScheduledTaskEngine engine = scope.ServiceProvider.GetRequiredService<IScheduledTaskEngine>();
        IScheduledTaskStoreResolver stores = scope.ServiceProvider.GetRequiredService<IScheduledTaskStoreResolver>();
        DateTime cutoff = clock.UtcNow - Grace;
        var linked = new Dictionary<string, HashSet<Guid>>(StringComparer.Ordinal);

        foreach (string schema in schemas)
        {
            IScheduledTaskStore store = stores.For(schema);

            try
            {
                linked[schema] = await RebuildTriggersAsync(store, engine, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogStepFailed(exception, "triggers", schema);
            }

            try
            {
                await EnqueueUnqueuedItemsAsync(store, engine, cutoff, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogStepFailed(exception, "run items", schema);
            }
        }

        try
        {
            await RemoveOrphansAsync(engine, linked, cutoff, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStepFailed(exception, "orphan triggers", string.Join(", ", schemas));
        }
    }

    /// <summary>Answers the trigger ids the schema's live tasks link once the rebuild is done.</summary>
    private async Task<HashSet<Guid>> RebuildTriggersAsync(IScheduledTaskStore store, IScheduledTaskEngine engine, CancellationToken ct)
    {
        var linked = new HashSet<Guid>();

        foreach (ScheduledTaskTriggerState state in await store.ListTriggersAsync(ct))
        {
            Guid? triggerId = state.EngineTriggerId;

            try
            {
                if (triggerId is not { } existing || !await engine.ExistsAsync(existing, ct))
                {
                    triggerId = await engine.ScheduleAsync(state.Trigger, ct);
                    await LinkAsync(store, state.Id, triggerId.Value, ct);
                    LogRecreated(state.Id.Value, store.Schema);
                }

                if (!state.Enabled)
                {
                    await engine.SetEnabledAsync(triggerId.Value, enabled: false, ct);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogTaskFailed(exception, state.Id.Value, store.Schema);
            }

            if (triggerId is { } kept)
            {
                linked.Add(kept);
            }
        }

        return linked;
    }

    private static async Task LinkAsync(IScheduledTaskStore store, ScheduledTaskId id, Guid triggerId, CancellationToken ct)
    {
        if (await store.GetTaskAsync(id, ignoreScope: true, ct) is { } task)
        {
            task.LinkTrigger(triggerId);
            await store.SaveAsync(ct);
        }
    }

    private async Task EnqueueUnqueuedItemsAsync(IScheduledTaskStore store, IScheduledTaskEngine engine, DateTime cutoff, CancellationToken ct)
    {
        foreach (Guid itemId in await store.ListUnqueuedRunItemsAsync(cutoff, ct))
        {
            Guid jobId = await engine.EnqueueRunItemAsync(new ScheduledRunItemReference(store.Schema, itemId), ct);

            if (await store.GetRunItemAsync(itemId, ct) is { } found)
            {
                found.Item.LinkJob(jobId);
                await store.SaveAsync(ct);
            }

            LogEnqueued(itemId, store.Schema);
        }
    }

    /// <summary>
    /// Only this host's schemas, and only those whose triggers were listed: a schema whose rows could not be read is
    /// left alone rather than having every trigger taken for an orphan, and another host's modules are not this host's.
    /// </summary>
    private async Task RemoveOrphansAsync(
        IScheduledTaskEngine engine, Dictionary<string, HashSet<Guid>> linked, DateTime cutoff, CancellationToken ct)
    {
        foreach (ScheduledEngineTrigger trigger in await engine.ListTriggersAsync(ct))
        {
            if (linked.TryGetValue(trigger.Schema, out HashSet<Guid>? ids)
                && !ids.Contains(trigger.EngineTriggerId)
                && trigger.CreatedAtUtc < cutoff)
            {
                await engine.RemoveAsync(trigger.EngineTriggerId, ct);
                LogOrphanRemoved(trigger.EngineTriggerId, trigger.Schema, trigger.TaskId.Value);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The scheduled-task rebuild failed for its {Step} step in schema(s) {Schema}; the next start tries again.")]
    private partial void LogStepFailed(Exception exception, string step, string schema);

    [LoggerMessage(Level = LogLevel.Error, Message = "The scheduled-task rebuild could not restore the trigger of task {TaskId} in schema {Schema}.")]
    private partial void LogTaskFailed(Exception exception, Guid taskId, string schema);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The job engine had lost the trigger of scheduled task {TaskId} in schema {Schema}; it was recreated.")]
    private partial void LogRecreated(Guid taskId, string schema);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scheduled run item {RunItemId} in schema {Schema} was never handed to the job engine; it was enqueued.")]
    private partial void LogEnqueued(Guid runItemId, string schema);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Removed job engine trigger {TriggerId} of schema {Schema}: no live scheduled task links it (task {TaskId}).")]
    private partial void LogOrphanRemoved(Guid triggerId, string schema, Guid taskId);
}
