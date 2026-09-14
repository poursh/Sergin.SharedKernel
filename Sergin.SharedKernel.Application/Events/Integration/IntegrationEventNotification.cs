using System.Collections.Concurrent;
using MediatR;

namespace Sergin.SharedKernel.Application.Events.Integration;

/// <summary>
/// The MediatR envelope around an integration event, carrying the identifiers a consumer needs but that the
/// event's own payload should not: <paramref name="MessageId"/> is the outbox row's id — not
/// <see cref="IIntegrationEvent.Id"/> — and what <see cref="IInbox{TUnitOfWork}"/> dedups on, and
/// <paramref name="CorrelationId"/> ties this delivery back to whatever caused it. Handlers never name this
/// type directly: they implement <see cref="IIntegrationEventHandler{TEvent}"/>, which unwraps it.
/// </summary>
public sealed record IntegrationEventNotification<TEvent>(Guid MessageId, string CorrelationId, TEvent Event) : INotification
    where TEvent : IIntegrationEvent;

public static class IntegrationEventNotification
{
    private static readonly ConcurrentDictionary<Type, Type> notificationTypes = new();

    /// <summary>
    /// Closes <see cref="IntegrationEventNotification{TEvent}"/> over the event's runtime type. The relay only
    /// ever holds an <see cref="IIntegrationEvent"/> after deserializing it, and MediatR resolves handlers by
    /// the notification's concrete type — so the envelope has to be built over
    /// <c>integrationEvent.GetType()</c>, not the interface, the same reason
    /// <c>DomainEventNotification.Wrap</c> exists one layer down.
    /// </summary>
    public static INotification Wrap(Guid messageId, string correlationId, IIntegrationEvent integrationEvent)
    {
        Type notificationType = notificationTypes.GetOrAdd(
            integrationEvent.GetType(),
            static eventType => typeof(IntegrationEventNotification<>).MakeGenericType(eventType));

        return (INotification)Activator.CreateInstance(notificationType, messageId, correlationId, integrationEvent)!;
    }
}
