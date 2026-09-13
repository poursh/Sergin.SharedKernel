using MediatR;

namespace Sergin.SharedKernel.Application.Events.Integration;

/// <summary>
/// The base for a consumer that needs effectively-once delivery: it records <c>(messageId, handler)</c> in
/// <typeparamref name="TUnitOfWork"/>'s inbox before running the handler's side effect, then saves once, so a
/// redelivered message is a no-op instead of applying its side effect twice. A handler that is naturally
/// idempotent can implement <see cref="IIntegrationEventHandler{TEvent}"/> directly instead and skip both the
/// inbox check and the save.
/// </summary>
public abstract class InboxIntegrationEventHandler<TEvent, TUnitOfWork>(IInbox<TUnitOfWork> inbox, TUnitOfWork unitOfWork)
    : IIntegrationEventHandler<TEvent>
    where TEvent : IIntegrationEvent
    where TUnitOfWork : IUnitOfWork
{
    async Task INotificationHandler<IntegrationEventNotification<TEvent>>.Handle(
        IntegrationEventNotification<TEvent> notification, CancellationToken cancellationToken)
    {
        if (!await inbox.TryRecordAsync(notification.MessageId, GetType().FullName ?? GetType().Name, cancellationToken))
        {
            return;
        }

        await Handle(notification.Event, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The handler's side effect. Runs at most once per <c>(messageId, handler)</c> pair; must not call
    /// <see cref="IUnitOfWork.SaveChangesAsync"/> itself — the base class saves once, after this returns, so
    /// the inbox row and the side effect commit together.
    /// </summary>
    public abstract Task Handle(TEvent integrationEvent, CancellationToken cancellationToken);
}
