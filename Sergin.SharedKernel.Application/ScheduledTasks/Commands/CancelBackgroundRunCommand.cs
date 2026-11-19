using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.ScheduledTasks.Activity;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Commands;

/// <summary>
/// Cancels a running run (a background, run-now or one-off one) in the store of the module that owns
/// <paramref name="Schema"/>: every item not yet started is Cancelled, and none is started after; an item already
/// running is left to its delivery. Final, never retried. The run's acting user may cancel it, and so may a holder of
/// <see cref="ScheduledTaskPermissions.Manage"/> on the run's scope.
/// </summary>
public sealed record CancelBackgroundRunCommand(string Schema, Guid RunId) : ICommand;

internal sealed class CancelBackgroundRunCommandHandler(
    IScheduledTaskStoreResolver stores,
    IUserContext caller,
    IScopeAuthorization auth,
    IDateTimeProvider clock,
    IRunNotifier notifier) : ICommandHandler<CancelBackgroundRunCommand>
{
    public async Task<ErrorOr<Success>> Handle(CancelBackgroundRunCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The schema is the caller's: one no module opted in under names no run, rather than a fault.
        if (!stores.TryFor(request.Schema, out IScheduledTaskStore? store)
            || await store.GetRunAsync(request.RunId, cancellationToken) is not { } run)
        {
            return Error.NotFound();
        }

        if (run.ActingUserId != caller.Id
            && !await auth.CurrentUserHasAsync(ScheduledTaskPermissions.Manage, run.ScopeId, cancellationToken))
        {
            return Error.Forbidden();
        }

        if (run.Status != ScheduledRunStatus.Running)
        {
            return ScheduledTaskErrors.RunFinished;
        }

        await store.CancelRunAsync(run.Id, clock.UtcNow, cancellationToken);

        // With every item cancelled, nothing else would ever complete the run; with one still running, its delivery will.
        await store.CompleteRunIfFinishedAsync(run.Id, clock.UtcNow, cancellationToken);

        notifier.Publish(new RunChanged(new BackgroundRunRef(store.Schema, run.Id), run.ActingUserId));

        return Result.Success;
    }
}
