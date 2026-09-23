using System.Reflection;

namespace Sergin.SharedKernel.Application.Aggregates;

/// <summary>
/// What every <see cref="IAggregateFeatureConfiguration{TAggregateRoot}"/> in a set of assemblies declared,
/// keyed by aggregate root type. Built twice, for two readers: each module <c>DbContext</c> builds its own from
/// its module's <c>.Application</c> assembly (through <c>SerginDbContext.AggregateFeatures</c>, which also works
/// at design time, where there is no DI container), and <c>AddSerginCore</c> registers one built from every
/// local module for the startup guard. Both builds refuse two configurations for one root and a configuration
/// without a parameterless constructor. Which entities are an aggregate's children is the EF model's
/// knowledge, not this registry's: it answers <see cref="ForChild"/> for whatever type it is asked about.
/// </summary>
public sealed class AggregateFeatureRegistry
{
    private readonly IReadOnlyDictionary<Type, Declaration> declarations;

    private AggregateFeatureRegistry(IReadOnlyDictionary<Type, Declaration> declarations)
    {
        this.declarations = declarations;
    }

    public static AggregateFeatureRegistry Empty { get; } = new(new Dictionary<Type, Declaration>());

    /// <summary>The configured aggregate root types.</summary>
    public IReadOnlyCollection<Type> ConfiguredTypes => [.. declarations.Keys];

    /// <summary>The features <paramref name="aggregateRoot"/> declared; None for an unconfigured root.</summary>
    public AggregateFeatures For(Type aggregateRoot)
    {
        ArgumentNullException.ThrowIfNull(aggregateRoot);

        return declarations.TryGetValue(aggregateRoot, out Declaration? declaration)
            ? declaration.Features
            : AggregateFeatures.None;
    }

    /// <summary>
    /// The features a child entity of <paramref name="aggregateRoot"/> takes: the root's, less any feature
    /// that excepts <paramref name="childType"/>.
    /// </summary>
    public AggregateFeatures ForChild(Type aggregateRoot, Type childType)
    {
        ArgumentNullException.ThrowIfNull(aggregateRoot);
        ArgumentNullException.ThrowIfNull(childType);

        if (!declarations.TryGetValue(aggregateRoot, out Declaration? declaration))
        {
            return AggregateFeatures.None;
        }

        return declaration.Features with
        {
            Audited = declaration.Features.Audited && !declaration.AuditExceptions.Contains(childType),
        };
    }

    /// <summary>
    /// Every child type <paramref name="aggregateRoot"/>'s configuration excepts from any feature, so the EF
    /// convention can refuse one that is not a child of that aggregate at all.
    /// </summary>
    public IReadOnlyCollection<Type> ExceptedChildren(Type aggregateRoot)
    {
        ArgumentNullException.ThrowIfNull(aggregateRoot);

        return declarations.TryGetValue(aggregateRoot, out Declaration? declaration)
            ? declaration.AuditExceptions
            : [];
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

        List<(Type RootType, Type ConfigurationType, Declaration Declaration)> declared = [];

        foreach (Type configurationType in configurationTypes)
        {
            foreach (Type closedInterface in configurationType.GetInterfaces().Where(IsClosedConfigurationInterface))
            {
                Type rootType = closedInterface.GetGenericArguments()[0];
                declared.Add((rootType, configurationType, Run(configurationType, closedInterface, rootType)));
            }
        }

        string[] duplicates =
        [
            .. declared
                .GroupBy(item => item.RootType)
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key.FullName} ({string.Join(", ", group.Select(item => item.ConfigurationType.FullName))})")
        ];

        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"More than one aggregate feature configuration is declared for: {string.Join("; ", duplicates)}. "
                + "Declare each aggregate's features in exactly one IAggregateFeatureConfiguration<T>.");
        }

        return new AggregateFeatureRegistry(declared.ToDictionary(item => item.RootType, item => item.Declaration));
    }

    private static Declaration Run(Type configurationType, Type closedInterface, Type rootType)
    {
        object configuration;

        try
        {
            configuration = Activator.CreateInstance(configurationType, nonPublic: true)!;
        }
        catch (MissingMethodException exception)
        {
            throw new InvalidOperationException(
                $"Aggregate feature configuration {configurationType.FullName} must have a parameterless constructor: "
                + "aggregate feature configurations are declarations, created without dependency injection.",
                exception);
        }

        var builder = (AggregateFeatureBuilder)Activator.CreateInstance(
            typeof(AggregateFeatureBuilder<>).MakeGenericType(rootType), nonPublic: true)!;

        // DoNotWrapExceptions: a refusal thrown inside Configure (ExceptChild of an aggregate root) reaches the
        // caller as itself, not wrapped in a TargetInvocationException.
        closedInterface
            .GetMethod(nameof(IAggregateFeatureConfiguration<>.Configure))!
            .Invoke(configuration, BindingFlags.DoNotWrapExceptions, binder: null, [builder], culture: null);

        return new Declaration(builder.Features, builder.AuditExceptions.ToHashSet());
    }

    private static bool IsConfigurationType(Type type) =>
        type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
        && type.GetInterfaces().Any(IsClosedConfigurationInterface);

    private static bool IsClosedConfigurationInterface(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAggregateFeatureConfiguration<>);

    private sealed record Declaration(AggregateFeatures Features, IReadOnlySet<Type> AuditExceptions);
}
