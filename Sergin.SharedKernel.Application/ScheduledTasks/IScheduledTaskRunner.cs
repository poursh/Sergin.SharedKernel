using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>
/// The only two calls a job-engine adapter makes into Sergin: a task's cron fired, and a run item's job is due.
/// Everything else (overlap, scope, identity, retry bookkeeping) is decided here, never by the engine.
/// </summary>
public interface IScheduledTaskRunner
{
    /// <summary>
    /// A task's trigger fired. Nothing when the task is gone, deleted or paused; a Skipped run while the previous one is
    /// unfinished; otherwise a Scheduled run, one item per target, acting for the task's delegating user in its scope.
    /// </summary>
    Task FireAsync(string schema, ScheduledTaskId taskId, CancellationToken ct);

    /// <summary>Executes one attempt of one run item; true when the engine should try it again later.</summary>
    Task<bool> ExecuteRunItemAsync(string schema, Guid runItemId, CancellationToken ct);
}
