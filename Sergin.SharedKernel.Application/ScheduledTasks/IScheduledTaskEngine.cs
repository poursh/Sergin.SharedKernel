using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>
/// What scheduled tasks need from a job engine, and nothing of the engine itself: a cron trigger per active task, and
/// one job per run item so retries and parallelism are the engine's. The implementation lives in its own adapter
/// assembly; no engine type ever appears here. The trigger's payload is only the schema and task id,
/// and a run item's only the schema and item id, so the engine's store can always be rebuilt from Sergin's rows.
/// </summary>
public interface IScheduledTaskEngine
{
    /// <summary>Registers a cron trigger for the task and answers the engine's id for it.</summary>
    Task<Guid> ScheduleAsync(ScheduledTrigger trigger, CancellationToken ct);

    Task RescheduleAsync(Guid engineTriggerId, ScheduledTrigger trigger, CancellationToken ct);

    Task SetEnabledAsync(Guid engineTriggerId, bool enabled, CancellationToken ct);

    Task RemoveAsync(Guid engineTriggerId, CancellationToken ct);

    Task<bool> ExistsAsync(Guid engineTriggerId, CancellationToken ct);

    /// <summary>Whether the trigger fires (true), is disabled (false), or is not held at all (null).</summary>
    Task<bool?> GetEnabledAsync(Guid engineTriggerId, CancellationToken ct);

    /// <summary>
    /// Every trigger the engine holds for scheduled tasks, whichever module's: what the start-up rebuild compares with
    /// the rows to remove the triggers no live task links.
    /// </summary>
    Task<IReadOnlyCollection<ScheduledEngineTrigger>> ListTriggersAsync(CancellationToken ct);

    /// <summary>Asks the engine to execute one run item now; answers the engine's job id.</summary>
    Task<Guid> EnqueueRunItemAsync(ScheduledRunItemReference item, CancellationToken ct);

    /// <summary>Whether the engine can fire <paramref name="cron"/> in <paramref name="timeZoneId"/>.</summary>
    bool IsValidCron(string cron, string timeZoneId);

    /// <summary>
    /// The first <paramref name="count"/> times after <paramref name="after"/> the engine would fire
    /// <paramref name="cron"/> in <paramref name="timeZoneId"/>, earliest first. Synchronous and pure (nothing is
    /// registered); empty exactly when <see cref="IsValidCron"/> refuses the pair. For previews in the UI.
    /// </summary>
    IReadOnlyList<DateTimeOffset> NextOccurrences(string cron, string timeZoneId, DateTimeOffset after, int count);
}

/// <summary>A task's trigger as the engine holds it: which module's task, and when it fires.</summary>
public sealed record ScheduledTrigger(string Schema, ScheduledTaskId TaskId, string Cron, string TimeZoneId);

/// <summary>A trigger the engine holds: its id, the task its payload names, and when the engine made it.</summary>
public sealed record ScheduledEngineTrigger(Guid EngineTriggerId, string Schema, ScheduledTaskId TaskId, DateTime CreatedAtUtc);

/// <summary>A run item as the engine holds it: which module's store, and which item.</summary>
public sealed record ScheduledRunItemReference(string Schema, Guid RunItemId);
