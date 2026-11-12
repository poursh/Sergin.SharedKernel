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

    /// <summary>
    /// Every live task's trigger, in every scope, active or paused: what the engine must hold, enabled exactly when the
    /// task is active. A deleted task is not listed.
    /// </summary>
    Task<IReadOnlyCollection<ScheduledTaskTriggerState>> ListTriggersAsync(CancellationToken ct);

    /// <summary>
    /// Starts an attempt of the item, if this delivery may: one conditional update moves a Pending or Retrying item, or a
    /// Running one quiet since before <paramref name="staleBefore"/> (its delivery died), to Running, counts the attempt
    /// and stamps the activity. True when this call claimed it; false when the item is finished, purged, or another
    /// delivery holds it. The row lock makes two concurrent claims of one item end with exactly one true.
    /// </summary>
    Task<bool> TryClaimRunItemAsync(Guid runItemId, DateTime now, DateTime staleBefore, CancellationToken ct);

    /// <summary>The unfinished run items quiet since before <paramref name="quietSince"/>: their jobs are taken for lost.</summary>
    Task<IReadOnlyCollection<Guid>> ListStaleRunItemsAsync(DateTime quietSince, CancellationToken ct);

    /// <summary>
    /// The runs still Running, started before <paramref name="quietSinceUtc"/>, with no item left pending, running or
    /// retrying: every item finished but the completion was lost (a fault or shutdown between the item's save and
    /// <see cref="CompleteRunIfFinishedAsync"/>). Left alone, such a run would make the overlap rule skip every later
    /// firing of its task.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> ListRunsToCompleteAsync(DateTime quietSinceUtc, CancellationToken ct);

    /// <summary>
    /// Links a stale item to the job the recovery just made for it and stamps the activity, unless something happened to
    /// it meanwhile (a delivery claimed or finished it); answers whether the row changed. A stale Running item becomes
    /// Retrying: its attempt was lost, and the stamp would otherwise make the new delivery take it for one still running.
    /// </summary>
    Task<bool> RelinkStaleRunItemAsync(Guid runItemId, Guid engineJobId, DateTime now, DateTime quietSince, CancellationToken ct);

    /// <summary>Deletes finished runs (and their items) completed before <paramref name="olderThanUtc"/>; answers how many runs.</summary>
    Task<int> PurgeAsync(DateTime olderThanUtc, CancellationToken ct);

    /// <summary>
    /// Saves a run just added. False when the store's one-running-run-per-task guarantee refused it (another run of the
    /// same task is Running); the pending changes are then discarded, so the caller can record a Skipped run instead.
    /// </summary>
    Task<bool> TrySaveStartedRunAsync(CancellationToken ct);

    Task SaveAsync(CancellationToken ct);
}

/// <summary>A live task's trigger as Sergin's row holds it, and whether the engine should fire it.</summary>
public sealed record ScheduledTaskTriggerState(ScheduledTaskId Id, Guid? EngineTriggerId, ScheduledTrigger Trigger, bool Enabled);
