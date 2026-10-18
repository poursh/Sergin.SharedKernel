using System.Reflection;
using Sergin.SharedKernel.Application.Aggregates;

namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>
/// A set of configuration types to put in the registry, registered as a singleton per assembly the way
/// <c>AssemblyIntegrationEventSource</c> is. <c>AddSerginCore</c> adds one per module ConfigurationsAssembly; a test
/// host adds <see cref="FromTypes"/> for its own requests, so it never scans a test assembly whole. It carries the
/// aggregate feature configurations too, because a request bound to an aggregate takes policy from its features.
/// </summary>
public sealed class CommandConfigurationSource
{
    private CommandConfigurationSource(IReadOnlyCollection<Type> configurationTypes, IReadOnlyCollection<Type> aggregateConfigurationTypes)
    {
        ConfigurationTypes = configurationTypes;
        AggregateConfigurationTypes = aggregateConfigurationTypes;
    }

    /// <summary>The command configuration types.</summary>
    public IReadOnlyCollection<Type> ConfigurationTypes { get; }

    /// <summary>The aggregate feature configuration types, read for the requests bound to an aggregate.</summary>
    public IReadOnlyCollection<Type> AggregateConfigurationTypes { get; }

    public static CommandConfigurationSource FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return new(
            [.. assembly.GetTypes().Where(CommandConfigurationRegistry.IsConfigurationType)],
            AggregateFeatureRegistry.ConfigurationTypesIn(assembly));
    }

    /// <summary>Command and aggregate feature configuration types, in any mix.</summary>
    public static CommandConfigurationSource FromTypes(params Type[] configurationTypes)
    {
        ArgumentNullException.ThrowIfNull(configurationTypes);

        return new(
            [.. configurationTypes.Where(type =>
                !AggregateFeatureRegistry.IsConfigurationType(type) || CommandConfigurationRegistry.IsConfigurationType(type))],
            [.. configurationTypes.Where(AggregateFeatureRegistry.IsConfigurationType)]);
    }
}
