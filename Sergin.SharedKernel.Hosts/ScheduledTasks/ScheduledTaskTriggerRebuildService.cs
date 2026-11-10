using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Application.ScheduledTasks;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Domain.ScheduledTasks;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.ScheduledTasks;

namespace Sergin.SharedKernel.Hosts.ScheduledTasks;

/// <summary>
/// Sergin's rows are the definition and the engine's store only their copy, so at host start the triggers are put back
/// in step, for every module that opted into scheduled tasks:
/// <list type="bullet">
/// <item>a live task whose trigger the engine does not hold (none linked, or an id the engine lost) gets a new one, and
/// the row links it;</item>
/// <item>every live task's trigger is enabled exactly when the task is active: a paused task's trigger is disabled, an
/// active task's disabled trigger enabled again (a pause or resume whose save failed after the engine call, and whose
/// undo failed too);</item>
/// <item>a trigger the engine holds for one of this host's schemas that no live task links, made more than
/// <see cref="Grace"/> ago, is removed: its task was deleted while the engine was unreachable, or never saved. The grace
/// keeps the rebuild off a create in flight, which schedules its trigger before it saves its row.</item>
/// </list>
/// Run items are not this service's: <see cref="ScheduledRunItemRecoveryService"/> hands over any unfinished item gone
/// quiet, all the time rather than only at start.
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
    /// <summary>How old an unlinked trigger must be before the rebuild treats it as left behind.</summary>
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
                Guid id;
                bool enabled;

                if (triggerId is { } existing && await engine.GetEnabledAsync(existing, ct) is { } held)
                {
                    id = existing;
                    enabled = held;
                }
                else
                {
                    // The engine's schedule call takes no enabled state, so a paused task's new trigger is created enabled
                    // and disabled at once below. A firing in between does nothing: the runner skips a paused task.
                    id = await engine.ScheduleAsync(state.Trigger, ct);
                    enabled = true;
                    triggerId = id;
                    await LinkAsync(store, state.Id, id, ct);
                    LogRecreated(state.Id.Value, store.Schema);
                }

                if (enabled != state.Enabled)
                {
                    await engine.SetEnabledAsync(id, state.Enabled, ct);
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Removed job engine trigger {TriggerId} of schema {Schema}: no live scheduled task links it (task {TaskId}).")]
    private partial void LogOrphanRemoved(Guid triggerId, string schema, Guid taskId);
}
