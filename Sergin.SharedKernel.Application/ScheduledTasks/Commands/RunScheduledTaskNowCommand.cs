using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Commands;

/// <summary>
/// Starts a run of a saved schedule now, outside its cron, acting for the caller (not the delegating user) in the
/// task's scope. Untyped: it changes nothing on the task, so it carries no version. It follows the overlap rule of a
/// scheduled firing: while a run of the task is unfinished, a Skipped run is recorded and
/// <see cref="ScheduledTaskErrors.Overlap"/> answered.
/// </summary>
public sealed record RunScheduledTaskNowCommand(string Schema, ScheduledTaskId Id) : ICommand<ScheduledRunStartedResponse>;

/// <summary>The run a run-now or run-once started.</summary>
public sealed record ScheduledRunStartedResponse(Guid RunId);

/// <summary>
/// The starter saves through the store, and on losing the one-running-run race clears the context's change tracker:
/// this handler has nothing else pending when it calls it, so nothing but the refused run is discarded.
/// </summary>
internal sealed class RunScheduledTaskNowCommandHandler(
    IScheduledTaskStoreResolver stores,
    ScheduledCommandCatalog catalog,
    ScheduledTaskAuthority authority,
    ScheduledRunStarter starter,
    IUserContext caller,
    IDateTimeProvider clock) : ICommandHandler<RunScheduledTaskNowCommand, ScheduledRunStartedResponse>
{
    public async Task<ErrorOr<ScheduledRunStartedResponse>> Handle(RunScheduledTaskNowCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        IScheduledTaskStore store = stores.For(request.Schema);

        if (await store.GetTaskAsync(request.Id, ignoreScope: false, cancellationToken) is not { } task)
        {
            return Error.NotFound();
        }

        if (!catalog.TryGet(task.CommandName, out ScheduledCommand? command))
        {
            return ScheduledTaskErrors.UnknownCommand;
        }

        ErrorOr<Success> allowed = await authority.EnsureMayScheduleAsync(command.RequestType, cancellationToken);

        if (allowed.IsError)
        {
            return allowed.Errors;
        }

        (Guid scopeId, string scopePath) = store.ScopeOf(task);
        var start = new ScheduledRunStart(
            task.Id, ScheduledRunTrigger.RunNow, caller.Id, scopeId, scopePath, task.CommandName, task.Arguments, [.. task.Targets]);

        if (!await store.HasUnfinishedRunAsync(task.Id, cancellationToken))
        {
            ErrorOr<Guid> started = await starter.StartAsync(store, start, cancellationToken);

            if (!started.IsError)
            {
                return new ScheduledRunStartedResponse(started.Value);
            }

            if (started.FirstError.Code != ScheduledTaskErrors.Overlap.Code)
            {
                return started.Errors;
            }
        }

        // Overlap, seen up front or lost to a concurrent start at the unique index: recorded, as a firing records it.
        var definition = new ScheduledRunDefinition(
            start.TaskId, start.Trigger, start.ActingUserId, start.ScopeId, start.ScopePath, start.CommandName, start.Arguments);
        store.AddRun(ScheduledTaskRun.Skipped(definition, clock.UtcNow), []);
        await store.SaveAsync(cancellationToken);

        return ScheduledTaskErrors.Overlap;
    }
}
