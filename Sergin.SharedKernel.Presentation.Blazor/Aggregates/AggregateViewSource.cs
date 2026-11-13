using System.Reflection;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// One module's aggregate view configurations and its schema, registered as a singleton the way
/// <c>ModuleConfigurationSource</c> is. <c>AddSerginBlazorApp</c> adds one per web UI module's UI assembly; a test adds
/// <see cref="FromTypes"/> for its own configurations, so it never scans a test assembly whole.
/// <see cref="AggregateViewRegistry"/> reads every source.
/// </summary>
public sealed class AggregateViewSource
{
    private AggregateViewSource(string schema, IReadOnlyCollection<Type> configurationTypes)
    {
        Schema = schema;
        ConfigurationTypes = configurationTypes;
    }

    /// <summary>The schema of the module whose views these are; each view model records it.</summary>
    public string Schema { get; }

    /// <summary>The <see cref="IAggregateViewConfiguration{TRoot}"/> implementations.</summary>
    public IReadOnlyCollection<Type> ConfigurationTypes { get; }

    /// <summary>Every view configuration declared in <paramref name="assembly"/>, a module's UI assembly.</summary>
    public static AggregateViewSource FromAssembly(Assembly assembly, string schema)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        return new AggregateViewSource(schema, [.. assembly.GetTypes().Where(IsConfigurationType)]);
    }

    /// <summary>The view configurations among <paramref name="configurationTypes"/>; other types are ignored.</summary>
    public static AggregateViewSource FromTypes(string schema, params Type[] configurationTypes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentNullException.ThrowIfNull(configurationTypes);

        return new AggregateViewSource(schema, [.. configurationTypes.Where(IsConfigurationType).Distinct()]);
    }

    internal static bool IsConfigurationType(Type type) =>
        type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
        && type.GetInterfaces().Any(IsClosedConfigurationInterface);

    internal static bool IsClosedConfigurationInterface(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAggregateViewConfiguration<>);
}
