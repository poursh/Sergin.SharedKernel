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

    /// <summary>Asks the engine to execute one run item now; answers the engine's job id.</summary>
    Task<Guid> EnqueueRunItemAsync(ScheduledRunItemReference item, CancellationToken ct);

    /// <summary>Whether the engine can fire <paramref name="cron"/> in <paramref name="timeZoneId"/>.</summary>
    bool IsValidCron(string cron, string timeZoneId);
}

/// <summary>A task's trigger as the engine holds it: which module's task, and when it fires.</summary>
public sealed record ScheduledTrigger(string Schema, ScheduledTaskId TaskId, string Cron, string TimeZoneId);

/// <summary>A run item as the engine holds it: which module's store, and which item.</summary>
public sealed record ScheduledRunItemReference(string Schema, Guid RunItemId);
