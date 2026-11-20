using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Domain.Tasks;

namespace Sergin.SharedKernel.Application.Tasks;

/// <summary>
/// <see cref="ITaskScheduleRunner"/>. A firing loads the task with the scope filter off: the engine's callback has
/// no caller, and a delegating user who lost reach must still produce a run, whose items then fail Forbidden or
/// NotFound one by one where the history shows it, instead of the schedule going silent. For the same reason a schedule
/// whose task is no longer <c>Schedulable()</c> fires a run that fails at once with <see cref="TaskErrors.NotSchedulable"/>:
/// the schedule stays listed, and its history says why it did nothing.
/// </summary>
internal sealed partial class TaskScheduleRunner(
    ITaskStoreResolver stores,
    TaskCatalog catalog,
    TaskRunStarter starter,
    TaskRunItemExecutor executor,
    IDateTimeProvider clock,
    ILogger<TaskScheduleRunner> logger) : ITaskScheduleRunner
{
    public async Task FireAsync(string schema, TaskScheduleId scheduleId, CancellationToken ct)
    {
        ITaskStore store = stores.For(schema);

        if (await store.GetTaskAsync(scheduleId, ignoreScope: true, ct) is not { State: TaskScheduleState.Active } task)
        {
            return;
        }

        (Guid scopeId, string scopePath) = store.ScopeOf(task);
        var definition = new TaskRunDefinition(
            task.Id, TaskRunTrigger.Schedule, task.DelegatingUserId, scopeId, scopePath, task.CommandName, task.Arguments);

        // A name no task holds any more is left to the run item, which fails it as UnknownCommand.
        if (catalog.ByName(task.CommandName) is { Settings.Schedulable: false })
        {
            LogNotSchedulable(task.Id.Value, task.CommandName);
            await AddFailedRunAsync(store, definition, task.Targets, ct);
            return;
        }

        if (!await store.HasUnfinishedRunAsync(task.Id, ct))
        {
            ErrorOr<Guid> started = await starter.StartAsync(
                store,
                new TaskRunStart(
                    task.Id, TaskRunTrigger.Schedule, task.DelegatingUserId, scopeId, scopePath, task.CommandName, task.Arguments, [.. task.Targets]),
                ct);

            if (!started.IsError)
            {
                return;
            }

            if (started.FirstError.Code != TaskErrors.Overlap.Code)
            {
                LogStartFailed(task.Id.Value, task.CommandName, string.Join("; ", started.Errors.Select(error => error.Code)));
                return;
            }
        }

        // Overlap, whether seen up front or lost to a concurrent firing at the unique index.
        store.AddRun(TaskRun.Skipped(definition, clock.UtcNow), []);
        await store.SaveAsync(ct);
    }

    /// <summary>A completed run whose every item failed <see cref="TaskErrors.NotSchedulable"/>, nothing enqueued.</summary>
    private async Task AddFailedRunAsync(
        ITaskStore store, TaskRunDefinition definition, IReadOnlyCollection<TaskScheduleTarget> targets, CancellationToken ct)
    {
        DateTime now = clock.UtcNow;
        var run = TaskRun.Start(definition, targets.Count, now);
        TaskRunItem[] items = [.. targets.Select(target => TaskRunItem.Create(run.Id, target.TargetId, target.RootId, now))];

        foreach (TaskRunItem item in items)
        {
            item.MarkFailed(TaskErrors.NotSchedulableCode, TaskErrors.NotSchedulable.Description, now);
        }

        run.Complete(0, items.Length, 0, now);
        store.AddRun(run, items);
        await store.SaveAsync(ct);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scheduled task {ScheduleId} ({CommandName}) fired but its task is no longer schedulable; the run failed.")]
    private partial void LogNotSchedulable(Guid scheduleId, string commandName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scheduled task {ScheduleId} ({CommandName}) fired but its run did not start: {Errors}.")]
    private partial void LogStartFailed(Guid scheduleId, string commandName, string errors);

    public Task<bool> ExecuteRunItemAsync(string schema, Guid runItemId, CancellationToken ct) =>
        executor.ExecuteAsync(schema, runItemId, ct);
}
