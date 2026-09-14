namespace Sergin.SharedKernel.Application.Events.Integration;

/// <summary>
/// The name↔type mapping behind <see cref="IntegrationEventNameAttribute"/>: the outbox stores the wire name,
/// never the CLR type name, so the writer calls <see cref="NameOf"/> to serialize a message and the relay
/// calls <see cref="TypeOf"/> to deserialize one. An implementation builds the map eagerly, from every
/// registered <see cref="IIntegrationEventSource"/>, so a missing or duplicate
/// <see cref="IntegrationEventNameAttribute"/> fails at host start rather than the first time that event is
/// actually produced or consumed; both members then throw <see cref="InvalidOperationException"/>, naming the
/// key that was not found, for a lookup the eager build did not already reject.
/// </summary>
public interface IIntegrationEventTypeRegistry
{
    string NameOf(Type eventType);

    Type TypeOf(string name);
}
