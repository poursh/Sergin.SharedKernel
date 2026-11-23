using MediatR;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Types;

namespace Sergin.SharedKernel.Application.Tasks;

/// <summary>
/// Refuses a task (<see cref="ITaskCommand"/>) sent inline, that is, outside a run item: a task is started, never sent.
/// Inside a run item only the item's own task request type passes, so a task's handler cannot send another task inline
/// (it would run under the first task's timeouts, progress and item, unrecorded as a run of its own). It reads the
/// request's type, not its configuration, so an unconfigured task is refused too. First in the pipeline, so nothing else
/// (permission lookups, validators) runs for a send that will be refused.
/// </summary>
internal sealed class TaskCommandGuardBehavior<TRequest, TResponse>(IRunItemContext runItem) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (request is ITaskCommand && (runItem.RunItemId is null || runItem.RequestType != request.GetType()))
        {
            return ErrorOrResponse.TryFrom(TaskErrors.MustRunAsTask, out TResponse refused)
                ? refused
                : throw new InvalidOperationException(TaskErrors.MustRunAsTask.Description);
        }

        return await next(cancellationToken);
    }
}
