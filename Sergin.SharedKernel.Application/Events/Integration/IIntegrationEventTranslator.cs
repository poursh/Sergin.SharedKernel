namespace Sergin.SharedKernel.Application.Events.Integration;

/// <summary>
/// The per-event decision about what leaks out of a module: given a domain event, produce the public,
/// primitive-only integration event that represents it. The outbox writer discovers translators by this
/// non-generic base so it can hold a heterogeneous collection keyed on a domain event's runtime type; a
/// domain event with no registered translator simply produces no outbox row. Written against
/// <see cref="IIntegrationEventTranslator{TDomainEvent}"/> instead of this interface directly.
/// </summary>
public interface IIntegrationEventTranslator
{
    IIntegrationEvent Translate(IDomainEvent domainEvent);
}

/// <summary>
/// The typed translator a module actually implements. The default interface method downcasts and forwards to
/// <c>Translate(TDomainEvent)</c>, so the writer can resolve translators by the non-generic
/// <see cref="IIntegrationEventTranslator"/> while an implementation only ever handles its one domain-typed
/// event.
/// </summary>
public interface IIntegrationEventTranslator<in TDomainEvent> : IIntegrationEventTranslator
    where TDomainEvent : IDomainEvent
{
    IIntegrationEvent Translate(TDomainEvent domainEvent);

    IIntegrationEvent IIntegrationEventTranslator.Translate(IDomainEvent domainEvent)
        => Translate((TDomainEvent)domainEvent);
}
