using MediatR;

namespace Sergin.SharedKernel.Application.Events;

/// <summary>
/// What a module implements to react to a domain event. It is an <see cref="INotificationHandler{TNotification}"/>
/// over the <see cref="DomainEventNotification{TEvent}"/> envelope, with the unwrapping done here once, so an
/// implementer writes <c>Handle(DeviceRegistered, CancellationToken)</c> and never sees the envelope — the same
/// way <c>ICommandHandler</c> hides <c>IRequestHandler</c>. <c>AddMediatR</c>'s assembly scan still finds
/// implementers through the inherited <c>INotificationHandler&lt;&gt;</c> interface.
/// <para>
/// Handlers run inside the originating <c>SaveChangesAsync</c>, before the transaction commits, on the same
/// scoped <c>DbContext</c>: whatever they add rides on that save, and an exception aborts it with nothing
/// written. They must not call <c>IUnitOfWork.SaveChangesAsync</c> themselves. The MediatR pipeline behaviors
/// (permission check, validation) wrap requests only and do not run here.
/// </para>
/// </summary>
public interface IDomainEventHandler<TEvent> : INotificationHandler<DomainEventNotification<TEvent>>
    where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken);

    Task INotificationHandler<DomainEventNotification<TEvent>>.Handle(
        DomainEventNotification<TEvent> notification, CancellationToken cancellationToken)
        => Handle(notification.Event, cancellationToken);
}
