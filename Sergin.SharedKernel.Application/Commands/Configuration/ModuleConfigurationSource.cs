using System.Reflection;
using Sergin.SharedKernel.Application.Aggregates;

namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>
/// One module's configurations: its command configurations, its aggregate feature configurations and its schema,
/// registered as a singleton per assembly the way <c>AssemblyIntegrationEventSource</c> is. <c>AddSerginCore</c> adds
/// one per module ConfigurationsAssembly; a test host adds <see cref="FromTypes"/> for its own requests, so it never
/// scans a test assembly whole. The command configuration registry and the permission catalogue both read every
/// source, so a test's configurations reach both.
/// </summary>
public sealed class ModuleConfigurationSource
{
    private ModuleConfigurationSource(
        IReadOnlyCollection<Type> configurationTypes,
        IReadOnlyCollection<Type> aggregateConfigurationTypes,
        IReadOnlyCollection<Type> requestTypes,
        string? schema,
        string? moduleName = null)
    {
        Schema = schema;
        ModuleName = moduleName;
        ConfigurationTypes = configurationTypes;
        AggregateConfigurationTypes = aggregateConfigurationTypes;
        RequestTypes = requestTypes;
    }

    /// <summary>
    /// The module schema every permission registered by this source's aggregates must belong to; null for a
    /// source that is not a module's (a test's <see cref="FromTypes"/>), which skips that check.
    /// </summary>
    public string? Schema { get; }

    /// <summary>
    /// The module's name (<c>DeviceManagement</c>), the first segment of the text keys its permission labels get when
    /// the aggregate names no text category; null for a source that is not a module's.
    /// </summary>
    public string? ModuleName { get; }

    /// <summary>The command configuration types.</summary>
    public IReadOnlyCollection<Type> ConfigurationTypes { get; }

    /// <summary>The aggregate feature configuration types, read for the typed requests on an aggregate.</summary>
    public IReadOnlyCollection<Type> AggregateConfigurationTypes { get; }

    /// <summary>
    /// The concrete requests (<see cref="IBaseCommand"/>) the source carries, configured or not: a typed record takes
    /// its root and version rule from its type, so the registry reads it even without a configuration.
    /// </summary>
    public IReadOnlyCollection<Type> RequestTypes { get; }

    /// <summary>
    /// The configurations in a module's configurations assembly and the requests in its contracts assembly, so a typed
    /// record with no configuration still gets its derived rules. The schema is required: it is what lets the catalogue
    /// refuse a permission registered outside its module.
    /// </summary>
    public static ModuleConfigurationSource FromAssemblies(
        Assembly configurationsAssembly, Assembly contractsAssembly, string schema, string? moduleName = null)
    {
        ArgumentNullException.ThrowIfNull(configurationsAssembly);
        ArgumentNullException.ThrowIfNull(contractsAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        return new(
            [.. configurationsAssembly.GetTypes().Where(CommandConfigurationRegistry.IsConfigurationType)],
            AggregateFeatureRegistry.ConfigurationTypesIn(configurationsAssembly),
            [.. contractsAssembly.GetTypes().Where(IsRequestType)],
            schema,
            moduleName);
    }

    /// <summary>Command and aggregate feature configuration types and request types, in any mix.</summary>
    public static ModuleConfigurationSource FromTypes(params Type[] configurationTypes)
    {
        ArgumentNullException.ThrowIfNull(configurationTypes);

        return new(
            [.. configurationTypes.Where(type =>
                !typeof(IBaseCommand).IsAssignableFrom(type)
                && (!AggregateFeatureRegistry.IsConfigurationType(type) || CommandConfigurationRegistry.IsConfigurationType(type)))],
            [.. configurationTypes.Where(AggregateFeatureRegistry.IsConfigurationType)],
            [.. configurationTypes.Where(IsRequestType)],
            schema: null);
    }

    /// <summary><see cref="FromTypes"/> that also states the module schema its permissions must belong to.</summary>
    public static ModuleConfigurationSource FromTypesInSchema(string schema, params Type[] configurationTypes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        ModuleConfigurationSource source = FromTypes(configurationTypes);
        return new(source.ConfigurationTypes, source.AggregateConfigurationTypes, source.RequestTypes, schema);
    }

    private static bool IsRequestType(Type type) =>
        type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } && typeof(IBaseCommand).IsAssignableFrom(type);
}
