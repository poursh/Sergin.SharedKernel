using MediatR;
using Sergin.SharedKernel.Application.Events;
using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Infrastructure.Events;

/// <summary>
/// Publishes a domain event to every registered <c>IDomainEventHandler&lt;TEvent&gt;</c> through MediatR's
/// <see cref="IPublisher"/> — zero or many handlers, run sequentially by the default publisher, stopping at the
/// first exception. Not <c>ISender.Send</c>: that expects exactly one <c>IRequest</c> handler and throws when
/// the type isn't a request or no handler exists, neither of which fits an event.
/// </summary>
internal sealed class DefaultEventDispatcher(IPublisher publisher) : IEventDispatcher
{
    public Task DispatchAsync(IDomainEvent @event, CancellationToken cancellationToken = default)
    {
        return publisher.Publish(DomainEventNotification.Wrap(@event), cancellationToken);
    }
}
