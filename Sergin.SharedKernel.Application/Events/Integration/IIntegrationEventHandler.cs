using MediatR;

namespace Sergin.SharedKernel.Application.Events.Integration;

/// <summary>
/// What a module implements to react to an integration event — the same unwrapping shape as
/// <see cref="Sergin.SharedKernel.Application.Events.IDomainEventHandler{TEvent}"/>: the assembly scan finds
/// implementers through the inherited <see cref="INotificationHandler{TNotification}"/> interface, and the
/// default interface method means an implementer only ever writes
/// <c>Handle(TEvent, CancellationToken)</c>, never naming the
/// <see cref="IntegrationEventNotification{TEvent}"/> envelope. Unlike a domain event handler, this one runs
/// in the relay's own scope and its own transaction, not inside the producer's <c>SaveChangesAsync</c>.
/// </summary>
public interface IIntegrationEventHandler<TEvent> : INotificationHandler<IntegrationEventNotification<TEvent>>
    where TEvent : IIntegrationEvent
{
    Task Handle(TEvent integrationEvent, CancellationToken cancellationToken);

    Task INotificationHandler<IntegrationEventNotification<TEvent>>.Handle(
        IntegrationEventNotification<TEvent> notification, CancellationToken cancellationToken)
        => Handle(notification.Event, cancellationToken);
}
