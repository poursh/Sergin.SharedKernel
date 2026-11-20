using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Tasks.Activity;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Domain.Tasks;

namespace Sergin.SharedKernel.Application.Tasks.Commands;

/// <summary>
/// Cancels a running run (a background, run-now or one-off one) in the store of the module that owns
/// <paramref name="Schema"/>: every item not yet started is Cancelled, and none is started after; an item already
/// running is left to its delivery. Final, never retried. The run's acting user may cancel it, and so may a holder of
/// <see cref="TaskPermissions.Manage"/> on the run's scope.
/// </summary>
public sealed record CancelTaskRunCommand(string Schema, Guid RunId) : ICommand;

internal sealed class CancelTaskRunCommandHandler(
    ITaskStoreResolver stores,
    IUserContext caller,
    IScopeAuthorization auth,
    IDateTimeProvider clock,
    IRunNotifier notifier) : ICommandHandler<CancelTaskRunCommand>
{
    public async Task<ErrorOr<Success>> Handle(CancelTaskRunCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The schema is the caller's: one no module opted in under names no run, rather than a fault.
        if (!stores.TryFor(request.Schema, out ITaskStore? store)
            || await store.GetRunAsync(request.RunId, cancellationToken) is not { } run)
        {
            return Error.NotFound();
        }

        if (run.ActingUserId != caller.Id
            && !await auth.CurrentUserHasAsync(TaskPermissions.Manage, run.ScopeId, cancellationToken))
        {
            return Error.Forbidden();
        }

        if (run.Status != TaskRunStatus.Running)
        {
            return TaskErrors.RunFinished;
        }

        await store.CancelRunAsync(run.Id, clock.UtcNow, cancellationToken);

        // With every item cancelled, nothing else would ever complete the run; with one still running, its delivery will.
        await store.CompleteRunIfFinishedAsync(run.Id, clock.UtcNow, cancellationToken);

        notifier.Publish(new RunChanged(new TaskRunRef(store.Schema, run.Id), run.ActingUserId));

        return Result.Success;
    }
}
