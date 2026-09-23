using System.Reflection;

namespace Sergin.SharedKernel.Application.Aggregates;

/// <summary>
/// What every <see cref="IAggregateConfiguration{TEntity}"/> in a set of assemblies declared, keyed by
/// entity type. Built twice, for two readers: each module <c>DbContext</c> builds its own from its module's
/// <c>.Application</c> assembly (through <c>SerginDbContext.AggregateFeatures</c>, which also works at
/// design time, where there is no DI container), and <c>AddSerginCore</c> registers one built from every
/// local module for the startup guard. Both builds refuse two configurations for one type and a
/// configuration without a parameterless constructor.
/// </summary>
public sealed class AggregateFeatureRegistry
{
    private readonly IReadOnlyDictionary<Type, AggregateFeatures> features;

    private AggregateFeatureRegistry(IReadOnlyDictionary<Type, AggregateFeatures> features)
    {
        this.features = features;
    }

    public static AggregateFeatureRegistry Empty { get; } = new(new Dictionary<Type, AggregateFeatures>());

    public IReadOnlyCollection<Type> ConfiguredTypes => [.. features.Keys];

    public AggregateFeatures For(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        return features.GetValueOrDefault(entityType, AggregateFeatures.None);
    }

    public static AggregateFeatureRegistry FromAssemblies(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        return FromConfigurationTypes(
            assemblies.SelectMany(assembly => assembly.GetTypes()).Where(IsConfigurationType));
    }

    public static AggregateFeatureRegistry FromConfigurationTypes(IEnumerable<Type> configurationTypes)
    {
        ArgumentNullException.ThrowIfNull(configurationTypes);

        List<(Type EntityType, Type ConfigurationType, AggregateFeatures Features)> declared = [];

        foreach (Type configurationType in configurationTypes)
        {
            foreach (Type closedInterface in configurationType.GetInterfaces().Where(IsClosedConfigurationInterface))
            {
                Type entityType = closedInterface.GetGenericArguments()[0];
                declared.Add((entityType, configurationType, Run(configurationType, closedInterface, entityType)));
            }
        }

        string[] duplicates =
        [
            .. declared
                .GroupBy(item => item.EntityType)
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key.FullName} ({string.Join(", ", group.Select(item => item.ConfigurationType.FullName))})")
        ];

        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"More than one aggregate configuration is declared for: {string.Join("; ", duplicates)}. "
                + "Declare each type's features in exactly one IAggregateConfiguration<T>.");
        }

        return new AggregateFeatureRegistry(declared.ToDictionary(item => item.EntityType, item => item.Features));
    }

    private static AggregateFeatures Run(Type configurationType, Type closedInterface, Type entityType)
    {
        object configuration;

        try
        {
            configuration = Activator.CreateInstance(configurationType, nonPublic: true)!;
        }
        catch (MissingMethodException exception)
        {
            throw new InvalidOperationException(
                $"Aggregate configuration {configurationType.FullName} must have a parameterless constructor: "
                + "aggregate configurations are declarations, created without dependency injection.",
                exception);
        }

        var builder = (AggregateFeatureBuilder)Activator.CreateInstance(
            typeof(AggregateFeatureBuilder<>).MakeGenericType(entityType), nonPublic: true)!;

        closedInterface
            .GetMethod(nameof(IAggregateConfiguration<IEntity>.Configure))!
            .Invoke(configuration, [builder]);

        return builder.Features;
    }

    private static bool IsConfigurationType(Type type) =>
        type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
        && type.GetInterfaces().Any(IsClosedConfigurationInterface);

    private static bool IsClosedConfigurationInterface(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAggregateConfiguration<>);
}
