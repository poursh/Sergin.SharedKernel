namespace Sergin.SharedKernel.Infrastructure.ScheduledTasks.TickerQ;

/// <summary>
/// A cron trigger's payload: which module's task fired, and nothing else, so the engine's store can always be rebuilt
/// from Sergin's rows. A plain <see cref="Guid"/> rather than the task's id type, so the stored JSON never depends on a
/// domain type's shape.
/// </summary>
public sealed record ScheduledTriggerRequest(string Schema, Guid TaskId);
