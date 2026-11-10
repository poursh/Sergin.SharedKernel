using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>
/// <see cref="IScheduledTaskRunner"/>. A firing loads the task with the scope filter off: the engine's callback has
/// no caller, and a delegating user who lost reach must still produce a run, whose items then fail Forbidden or
/// NotFound one by one where the history shows it, instead of the schedule going silent.
/// </summary>
internal sealed partial class ScheduledTaskRunner(
    IScheduledTaskStoreResolver stores,
    ScheduledRunStarter starter,
    ScheduledRunItemExecutor executor,
    IDateTimeProvider clock,
    ILogger<ScheduledTaskRunner> logger) : IScheduledTaskRunner
{
    public async Task FireAsync(string schema, ScheduledTaskId taskId, CancellationToken ct)
    {
        IScheduledTaskStore store = stores.For(schema);

        if (await store.GetTaskAsync(taskId, ignoreScope: true, ct) is not { State: ScheduledTaskState.Active } task)
        {
            return;
        }

        (Guid scopeId, string scopePath) = store.ScopeOf(task);
        var definition = new ScheduledRunDefinition(
            task.Id, ScheduledRunTrigger.Scheduled, task.DelegatingUserId, scopeId, scopePath, task.CommandName, task.Arguments);

        if (!await store.HasUnfinishedRunAsync(task.Id, ct))
        {
            ErrorOr<Guid> started = await starter.StartAsync(
                store,
                new ScheduledRunStart(
                    task.Id, ScheduledRunTrigger.Scheduled, task.DelegatingUserId, scopeId, scopePath, task.CommandName, task.Arguments, [.. task.Targets]),
                ct);

            if (!started.IsError)
            {
                return;
            }

            if (started.FirstError != ScheduledTaskErrors.Overlap)
            {
                LogStartFailed(task.Id.Value, task.CommandName, string.Join("; ", started.Errors.Select(error => error.Code)));
                return;
            }
        }

        // Overlap, whether seen up front or lost to a concurrent firing at the unique index.
        store.AddRun(ScheduledTaskRun.Skipped(definition, clock.UtcNow), []);
        await store.SaveAsync(ct);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scheduled task {TaskId} ({CommandName}) fired but its run did not start: {Errors}.")]
    private partial void LogStartFailed(Guid taskId, string commandName, string errors);

    public Task<bool> ExecuteRunItemAsync(string schema, Guid runItemId, CancellationToken ct) =>
        executor.ExecuteAsync(schema, runItemId, ct);
}
