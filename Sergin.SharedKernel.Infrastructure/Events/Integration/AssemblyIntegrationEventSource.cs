using System.Reflection;
using Sergin.SharedKernel.Application.Events.Integration;

namespace Sergin.SharedKernel.Infrastructure.Events.Integration;

/// <summary>
/// Scans an assembly — normally a module's <c>.Application.Contracts</c> project — for every integration
/// event it declares, so a module never hand-lists its own event types for registration. Filters to
/// concrete, non-generic, top-level classes implementing <see cref="IIntegrationEvent"/>: nested types are
/// excluded because a type nested inside another (a test fixture's private event type, for instance) is an
/// implementation detail rather than part of the module's public wire contract, and stays reachable only
/// through <see cref="TypesIntegrationEventSource"/> when a caller means to register it explicitly.
/// </summary>
public sealed class AssemblyIntegrationEventSource(Assembly assembly) : IIntegrationEventSource
{
    public IEnumerable<Type> EventTypes => assembly.GetTypes().Where(IsIntegrationEventType);

    private static bool IsIntegrationEventType(Type type) =>
        type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false, IsNested: false }
        && typeof(IIntegrationEvent).IsAssignableFrom(type);
}
