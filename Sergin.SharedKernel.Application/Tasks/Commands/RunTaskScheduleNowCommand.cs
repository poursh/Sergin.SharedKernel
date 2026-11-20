using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Domain.Tasks;

namespace Sergin.SharedKernel.Application.Tasks.Commands;

/// <summary>
/// Starts a run of a saved schedule now, outside its cron, acting for the caller (not the delegating user) in the
/// task's scope. Untyped: it changes nothing on the task, so it carries no version. It follows the overlap rule of a
/// scheduled firing: while a run of the task is unfinished, a Skipped run is recorded and
/// <see cref="TaskErrors.Overlap"/> answered.
/// </summary>
public sealed record RunTaskScheduleNowCommand(string Schema, TaskScheduleId Id) : ICommand<TaskScheduleRunStartedResponse>;

/// <summary>The run a run-now or run-once started.</summary>
public sealed record TaskScheduleRunStartedResponse(Guid RunId);

/// <summary>
/// The starter saves through the store, and on losing the one-running-run race clears the context's change tracker:
/// this handler has nothing else pending when it calls it, so nothing but the refused run is discarded.
/// </summary>
internal sealed class RunTaskScheduleNowCommandHandler(
    ITaskStoreResolver stores,
    TaskCatalog catalog,
    TaskAuthority authority,
    TaskRunStarter starter,
    IUserContext caller,
    IDateTimeProvider clock) : ICommandHandler<RunTaskScheduleNowCommand, TaskScheduleRunStartedResponse>
{
    public async Task<ErrorOr<TaskScheduleRunStartedResponse>> Handle(RunTaskScheduleNowCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ITaskStore store = stores.For(request.Schema);

        if (await store.GetTaskAsync(request.Id, ignoreScope: false, cancellationToken) is not { } task)
        {
            return Error.NotFound();
        }

        if (catalog.ByName(task.CommandName) is not { } command)
        {
            return TaskErrors.UnknownCommand;
        }

        // A schedule saved while its task was Schedulable() outlives the call; it is kept, but no longer runs.
        if (!command.Settings.Schedulable)
        {
            return TaskErrors.NotSchedulable;
        }

        ErrorOr<Success> allowed = await authority.EnsureMayScheduleAsync(command.RequestType, cancellationToken);

        if (allowed.IsError)
        {
            return allowed.Errors;
        }

        (Guid scopeId, string scopePath) = store.ScopeOf(task);
        var start = new TaskRunStart(
            task.Id, TaskRunTrigger.ScheduleNow, caller.Id, scopeId, scopePath, task.CommandName, task.Arguments, [.. task.Targets]);

        if (!await store.HasUnfinishedRunAsync(task.Id, cancellationToken))
        {
            ErrorOr<Guid> started = await starter.StartAsync(store, start, cancellationToken);

            if (!started.IsError)
            {
                return new TaskScheduleRunStartedResponse(started.Value);
            }

            if (started.FirstError.Code != TaskErrors.Overlap.Code)
            {
                return started.Errors;
            }
        }

        // Overlap, seen up front or lost to a concurrent start at the unique index: recorded, as a firing records it.
        var definition = new TaskRunDefinition(
            start.ScheduleId, start.Trigger, start.ActingUserId, start.ScopeId, start.ScopePath, start.CommandName, start.Arguments);
        store.AddRun(TaskRun.Skipped(definition, clock.UtcNow), []);
        await store.SaveAsync(cancellationToken);

        return TaskErrors.Overlap;
    }
}
