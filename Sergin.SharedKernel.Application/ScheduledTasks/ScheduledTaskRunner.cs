using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>
/// <see cref="IScheduledTaskRunner"/>. A firing loads the task with the scope filter off: the engine's callback has
/// no caller, and a delegating user who lost reach must still produce a run, whose items then fail Forbidden or
/// NotFound one by one where the history shows it, instead of the schedule going silent.
/// </summary>
internal sealed class ScheduledTaskRunner(
    IScheduledTaskStoreResolver stores,
    ScheduledRunStarter starter,
    ScheduledRunItemExecutor executor,
    IDateTimeProvider clock) : IScheduledTaskRunner
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

        if (await store.HasUnfinishedRunAsync(task.Id, ct))
        {
            store.AddRun(ScheduledTaskRun.Skipped(definition, clock.UtcNow), []);
            await store.SaveAsync(ct);

            return;
        }

        await starter.StartAsync(
            store,
            new ScheduledRunStart(
                task.Id, ScheduledRunTrigger.Scheduled, task.DelegatingUserId, scopeId, scopePath, task.CommandName, task.Arguments, [.. task.Targets]),
            ct);
    }

    public Task<bool> ExecuteRunItemAsync(string schema, Guid runItemId, CancellationToken ct) =>
        executor.ExecuteAsync(schema, runItemId, ct);
}
