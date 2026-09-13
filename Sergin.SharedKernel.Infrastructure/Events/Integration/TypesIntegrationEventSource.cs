using Sergin.SharedKernel.Application.Events.Integration;

namespace Sergin.SharedKernel.Infrastructure.Events.Integration;

/// <summary>
/// An explicit list of integration event types, for a caller that wants exact control instead of an assembly
/// scan — a test suite registering a handful of test-only event types, or any caller registering a type
/// <see cref="AssemblyIntegrationEventSource"/> would filter out on purpose, such as one nested inside
/// another class.
/// </summary>
public sealed class TypesIntegrationEventSource(params Type[] eventTypes) : IIntegrationEventSource
{
    public IEnumerable<Type> EventTypes => eventTypes;
}
