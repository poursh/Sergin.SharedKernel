using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>
/// One opted-in module's scheduled-task tables. Scoped, one per schema, over that module's own context; reach it
/// through <see cref="IScheduledTaskStoreResolver"/>. Changes are tracked until <see cref="SaveAsync"/>.
/// </summary>
public interface IScheduledTaskStore
{
    string Schema { get; }

    /// <summary>
    /// The live task, or null. With <paramref name="ignoreScope"/> false the scope filter applies, as for any scoped
    /// aggregate; the engine's callback passes true, since it runs before any caller's reach is known.
    /// </summary>
    Task<ScheduledTask?> GetTaskAsync(ScheduledTaskId id, bool ignoreScope, CancellationToken ct);

    /// <summary>
    /// The scope id and path stamped on a tracked task. They are columns of the scoped-aggregate feature, not
    /// properties of the domain type, so the store reads them.
    /// </summary>
    (Guid ScopeId, string ScopePath) ScopeOf(ScheduledTask task);

    void AddTask(ScheduledTask task);

    void RemoveTask(ScheduledTask task);

    void AddRun(ScheduledTaskRun run, IReadOnlyCollection<ScheduledTaskRunItem> items);

    /// <summary>Whether a run of the task still has items to finish: the overlap rule's question.</summary>
    Task<bool> HasUnfinishedRunAsync(ScheduledTaskId id, CancellationToken ct);

    /// <summary>The run item and its run, tracked; null when either is gone (purged).</summary>
    Task<(ScheduledTaskRun Run, ScheduledTaskRunItem Item)?> GetRunItemAsync(Guid runItemId, CancellationToken ct);

    /// <summary>Completes the run with its counts once no item is still pending, running or retrying; saves.</summary>
    Task CompleteRunIfFinishedAsync(Guid runId, DateTime at, CancellationToken ct);

    /// <summary>Every live, active task's trigger, in every scope: what the engine must hold.</summary>
    Task<IReadOnlyCollection<(ScheduledTaskId Id, Guid? EngineTriggerId, ScheduledTrigger Trigger)>> ListActiveTriggersAsync(CancellationToken ct);

    /// <summary>Deletes finished runs (and their items) completed before <paramref name="olderThanUtc"/>; answers how many runs.</summary>
    Task<int> PurgeAsync(DateTime olderThanUtc, CancellationToken ct);

    /// <summary>
    /// Saves a run just added. False when the store's one-running-run-per-task guarantee refused it (another run of the
    /// same task is Running); the pending changes are then discarded, so the caller can record a Skipped run instead.
    /// </summary>
    Task<bool> TrySaveStartedRunAsync(CancellationToken ct);

    Task SaveAsync(CancellationToken ct);
}
