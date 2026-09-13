using System.Collections.Concurrent;
using MediatR;

namespace Sergin.SharedKernel.Application.Events;

/// <summary>
/// The MediatR envelope around a domain event. <see cref="IDomainEvent"/> lives in <c>Sergin.SharedKernel.Domain</c>,
/// which has no MediatR reference on purpose, so the <see cref="INotification"/> shape is added here — one
/// layer up, the same place <c>ICommand</c> wraps <c>IRequest</c>. Handlers never name this type: they
/// implement <see cref="IDomainEventHandler{TEvent}"/>, which unwraps it.
/// </summary>
public sealed record DomainEventNotification<TEvent>(TEvent Event) : INotification
    where TEvent : IDomainEvent;

public static class DomainEventNotification
{
    private static readonly ConcurrentDictionary<Type, Type> notificationTypes = new();

    /// <summary>
    /// Closes <see cref="DomainEventNotification{TEvent}"/> over the event's runtime type. By the time the
    /// interceptor holds an event it only has <see cref="IDomainEvent"/>, and MediatR resolves handlers by the
    /// notification's concrete type — so the envelope has to be built over <c>domainEvent.GetType()</c>, not
    /// the interface.
    /// </summary>
    public static INotification Wrap(IDomainEvent domainEvent)
    {
        Type notificationType = notificationTypes.GetOrAdd(
            domainEvent.GetType(),
            static eventType => typeof(DomainEventNotification<>).MakeGenericType(eventType));

        return (INotification)Activator.CreateInstance(notificationType, domainEvent)!;
    }
}
