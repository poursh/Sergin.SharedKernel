using System.Collections.Frozen;
using System.Reflection;
using Sergin.SharedKernel.Application.Events.Integration;

namespace Sergin.SharedKernel.Infrastructure.Events.Integration;

/// <summary>
/// Builds the name↔type map once, at construction, from every registered <see cref="IIntegrationEventSource"/>,
/// so a missing or duplicate <see cref="IntegrationEventNameAttribute"/> fails host start — before any
/// message is ever produced or consumed — rather than surfacing as a serialization failure the first time
/// that event is used.
/// </summary>
internal sealed class IntegrationEventTypeRegistry : IIntegrationEventTypeRegistry
{
    private readonly FrozenDictionary<Type, string> namesByType;
    private readonly FrozenDictionary<string, Type> typesByName;

    public IntegrationEventTypeRegistry(IEnumerable<IIntegrationEventSource> sources)
    {
        IReadOnlyCollection<(Type Type, IntegrationEventNameAttribute? Attribute)> types = [.. sources
            .SelectMany(source => source.EventTypes)
            .Select(type => (Type: type, Attribute: type.GetCustomAttribute<IntegrationEventNameAttribute>()))];

        IReadOnlyCollection<Type> missingName = [.. types.Where(entry => entry.Attribute is null).Select(entry => entry.Type)];

        IReadOnlyCollection<IGrouping<string, Type>> duplicateNames = [.. types
            .Where(entry => entry.Attribute is not null)
            .GroupBy(entry => entry.Attribute!.Name, entry => entry.Type, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)];

        if (missingName.Count > 0 || duplicateNames.Count > 0)
        {
            throw new InvalidOperationException(BuildFailureMessage(missingName, duplicateNames));
        }

        namesByType = types.ToFrozenDictionary(entry => entry.Type, entry => entry.Attribute!.Name);
        typesByName = namesByType.ToFrozenDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);
    }

    public string NameOf(Type eventType) =>
        namesByType.TryGetValue(eventType, out string? name)
            ? name
            : throw new InvalidOperationException($"'{eventType.FullName}' is not a registered integration event type.");

    public Type TypeOf(string name) =>
        typesByName.TryGetValue(name, out Type? type)
            ? type
            : throw new InvalidOperationException($"'{name}' is not a registered integration event name.");

    private static string BuildFailureMessage(
        IReadOnlyCollection<Type> missingName,
        IReadOnlyCollection<IGrouping<string, Type>> duplicateNames)
    {
        List<string> problems = [];

        if (missingName.Count > 0)
        {
            problems.Add($"missing [IntegrationEventName]: {string.Join(", ", missingName.Select(type => type.FullName))}");
        }

        problems.AddRange(duplicateNames.Select(group => $"{group.Key}: {string.Join(", ", group.Select(type => type.FullName))}"));

        return "Every integration event type must carry a unique [IntegrationEventName(\"…\")] attribute. " + string.Join("; ", problems);
    }
}
