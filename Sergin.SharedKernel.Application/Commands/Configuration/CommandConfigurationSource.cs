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
    private CommandConfigurationSource(IReadOnlyCollection<Type> configurationTypes, IReadOnlyCollection<Type> aggregateConfigurationTypes, string? schema)
    {
        Schema = schema;
        ConfigurationTypes = configurationTypes;
        AggregateConfigurationTypes = aggregateConfigurationTypes;
    }

    /// <summary>
    /// The module schema every permission registered by this source's aggregates must belong to; null for a
    /// source that is not a module's (a test's <see cref="FromTypes"/>), which skips that check.
    /// </summary>
    public string? Schema { get; }

    /// <summary>The command configuration types.</summary>
    public IReadOnlyCollection<Type> ConfigurationTypes { get; }

    /// <summary>The aggregate feature configuration types, read for the requests bound to an aggregate.</summary>
    public IReadOnlyCollection<Type> AggregateConfigurationTypes { get; }

    /// <summary>
    /// The configurations in a module's configurations assembly. The schema is required: it is what lets the
    /// catalogue refuse a permission registered outside its module.
    /// </summary>
    public static CommandConfigurationSource FromAssembly(Assembly assembly, string schema)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        return new(
            [.. assembly.GetTypes().Where(CommandConfigurationRegistry.IsConfigurationType)],
            AggregateFeatureRegistry.ConfigurationTypesIn(assembly),
            schema);
    }

    /// <summary>Command and aggregate feature configuration types, in any mix.</summary>
    public static CommandConfigurationSource FromTypes(params Type[] configurationTypes)
    {
        ArgumentNullException.ThrowIfNull(configurationTypes);

        return new(
            [.. configurationTypes.Where(type =>
                !AggregateFeatureRegistry.IsConfigurationType(type) || CommandConfigurationRegistry.IsConfigurationType(type))],
            [.. configurationTypes.Where(AggregateFeatureRegistry.IsConfigurationType)],
            schema: null);
    }

    /// <summary><see cref="FromTypes"/> that also states the module schema its permissions must belong to.</summary>
    public static CommandConfigurationSource FromTypesInSchema(string schema, params Type[] configurationTypes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        CommandConfigurationSource source = FromTypes(configurationTypes);
        return new(source.ConfigurationTypes, source.AggregateConfigurationTypes, schema);
    }
}
