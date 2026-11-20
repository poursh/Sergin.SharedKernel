using MediatR;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Configuration;

namespace Sergin.SharedKernel.Application.Tasks;

/// <summary>
/// Refuses a task (<see cref="CommandSettings.Task"/>) that is sent inline, that is, outside a run item: it is started, never sent.
/// First in the pipeline, so nothing else (permission lookups, validators) runs for a send that will be refused.
/// </summary>
internal sealed class BackgroundCommandGuardBehavior<TRequest, TResponse>(
    CommandConfigurationRegistry configurations,
    IRunItemContext runItem) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (runItem.RunItemId is null && configurations.For(request.GetType()).Task is not null)
        {
            return ErrorOrResponse.TryFrom(TaskErrors.MustRunInBackground, out TResponse refused)
                ? refused
                : throw new InvalidOperationException(TaskErrors.MustRunInBackground.Description);
        }

        return await next(cancellationToken);
    }
}
