using System.Reflection;
using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Application.Commands.Configuration;
using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// One <see cref="AggregateViewModel"/> per root a view configuration declares, built once per host from every
/// registered <see cref="AggregateViewSource"/>: the root's typed requests in <see cref="CommandConfigurationRegistry"/>
/// joined with their policy, the root's features and texts, and the configuration's presentation facts. Building it
/// refuses two configurations for one root, a configuration without a parameterless constructor or whose Configure
/// throws, and every configuration mistake the requests can show (see <see cref="AggregateViewGuard"/>); the routes are
/// checked at start by <see cref="AggregateViewGuard.Ensure"/>.
/// </summary>
public sealed class AggregateViewRegistry
{
    private readonly IReadOnlyDictionary<Type, AggregateViewModel> models;

    private AggregateViewRegistry(IReadOnlyDictionary<Type, AggregateViewModel> models) => this.models = models;

    public static AggregateViewRegistry Empty { get; } = new(new Dictionary<Type, AggregateViewModel>());

    /// <summary>Every configured root's model.</summary>
    public IReadOnlyCollection<AggregateViewModel> All => [.. models.Values];

    /// <summary>The model of <typeparamref name="TRoot"/>; refuses a root no view configuration declares.</summary>
    public AggregateViewModel For<TRoot>()
        where TRoot : class, IAggregateRoot =>
        Find(typeof(TRoot))
        ?? throw new InvalidOperationException(
            $"No aggregate view configuration declares {typeof(TRoot).FullName}. Add an IAggregateViewConfiguration<{typeof(TRoot).Name}> "
            + "to its module's UI assembly.");

    /// <summary>The model of <paramref name="root"/>, or null when no view configuration declares it.</summary>
    public AggregateViewModel? Find(Type root)
    {
        ArgumentNullException.ThrowIfNull(root);

        return models.GetValueOrDefault(root);
    }

    public static AggregateViewRegistry Build(
        IEnumerable<AggregateViewSource> sources,
        CommandConfigurationRegistry commands,
        AggregateFeatureRegistry aggregates,
        TextCatalog texts)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(aggregates);
        ArgumentNullException.ThrowIfNull(texts);

        List<DeclaredView> declared = [];
        HashSet<Type> seen = [];

        foreach (AggregateViewSource source in sources)
        {
            foreach (Type configurationType in source.ConfigurationTypes.Where(seen.Add))
            {
                foreach (Type closedInterface in configurationType.GetInterfaces().Where(AggregateViewSource.IsClosedConfigurationInterface))
                {
                    declared.Add(Run(configurationType, closedInterface, source.Schema));
                }
            }
        }

        AggregateViewGuard.EnsureOnePerRoot(declared);

        var targets = AggregateTargets.From(commands.RequestTypes);

        return new AggregateViewRegistry(declared.ToDictionary(
            view => view.Builder.Root,
            view => new AggregateViewModelJoin(view, commands, aggregates, texts, targets).Build()));
    }

    private static DeclaredView Run(Type configurationType, Type closedInterface, string schema)
    {
        Type root = closedInterface.GetGenericArguments()[0];
        object configuration;

        try
        {
            configuration = Activator.CreateInstance(configurationType, nonPublic: true)!;
        }
        catch (MissingMethodException exception)
        {
            throw new InvalidOperationException(
                $"Aggregate view configuration {configurationType.FullName} must have a parameterless constructor: "
                + "view configurations are declarations, created without dependency injection.",
                exception);
        }

        var builder = (AggregateViewBuilder)Activator.CreateInstance(
            typeof(AggregateViewBuilder<>).MakeGenericType(root), nonPublic: true)!;

        try
        {
            closedInterface
                .GetMethod(nameof(IAggregateViewConfiguration<>.Configure))!
                .Invoke(configuration, BindingFlags.DoNotWrapExceptions, binder: null, [builder], culture: null);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Aggregate view configuration {configurationType.FullName} failed while configuring {root.FullName}: {exception.Message}",
                exception);
        }

        return new DeclaredView(configurationType, schema, builder);
    }
}

/// <summary>One configuration's run: its type, its source's schema and what its builder collected.</summary>
internal sealed record DeclaredView(Type Configuration, string Schema, AggregateViewBuilder Builder);
