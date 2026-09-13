namespace Sergin.SharedKernel.Application.Events.Integration;

/// <summary>
/// Where <see cref="IIntegrationEventTypeRegistry"/> gets the set of types it must be able to name — one
/// instance per local or remote module's contracts assembly in production, or an explicit hand-picked list in
/// a test suite. Registered as a collection so the registry can validate every source's types together at
/// construction, rather than accepting whichever module happened to register first.
/// </summary>
public interface IIntegrationEventSource
{
    IEnumerable<Type> EventTypes { get; }
}
