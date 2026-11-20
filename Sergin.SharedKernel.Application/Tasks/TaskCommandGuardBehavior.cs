using MediatR;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Types;

namespace Sergin.SharedKernel.Application.Tasks;

/// <summary>
/// Refuses a task (<see cref="ITaskCommand"/>) sent inline, that is, outside a run item: a task is started, never sent.
/// It reads the request's type, not its configuration, so an unconfigured task is refused too. First in the pipeline,
/// so nothing else (permission lookups, validators) runs for a send that will be refused.
/// </summary>
internal sealed class TaskCommandGuardBehavior<TRequest, TResponse>(IRunItemContext runItem) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (request is ITaskCommand && runItem.RunItemId is null)
        {
            return ErrorOrResponse.TryFrom(TaskErrors.MustRunAsTask, out TResponse refused)
                ? refused
                : throw new InvalidOperationException(TaskErrors.MustRunAsTask.Description);
        }

        return await next(cancellationToken);
    }
}
