namespace Sergin.SharedKernel.Application.Events.Integration;

/// <summary>
/// The stable wire name for an integration event, e.g. <c>"dm.device.registered.v1"</c> — what the outbox
/// stores in place of the CLR type name, so a producer can rename or move the class without breaking a
/// consumer that has already stored messages under the old name. Every type an
/// <see cref="IIntegrationEventSource"/> yields must carry one with a name unique across every source, or
/// <see cref="IIntegrationEventTypeRegistry"/> refuses to build.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IntegrationEventNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
