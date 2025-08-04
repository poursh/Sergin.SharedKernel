using MediatR;
using Sergin.SharedKernel.Application.Events;
using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Infrastructure.Events;

internal sealed class DefaultEventDispatcher(ISender sender) : IEventDispatcher
{
    public Task DispatchAsync(IDomainEvent @event, CancellationToken cancellationToken = default)
    {
        return sender.Send(@event, cancellationToken);
    }
}
