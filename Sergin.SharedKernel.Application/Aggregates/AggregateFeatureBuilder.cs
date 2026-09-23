namespace Sergin.SharedKernel.Application.Aggregates;

/// <summary>
/// The non-generic half of the builder, so the registry can read what a configuration declared without
/// reflecting over the generic type.
/// </summary>
public abstract class AggregateFeatureBuilder
{
    private protected AggregateFeatureBuilder()
    {
    }

    internal AggregateFeatures Features { get; private protected set; } = AggregateFeatures.None;

    /// <summary>The child entity types excepted from <see cref="AggregateFeatures.Audited"/>.</summary>
    internal HashSet<Type> AuditExceptions { get; } = [];
}

public sealed class AggregateFeatureBuilder<TAggregateRoot> : AggregateFeatureBuilder
    where TAggregateRoot : class, IAggregateRoot
{
    internal AggregateFeatureBuilder()
    {
    }

    /// <summary>
    /// Adds created/modified stamps to the root's table and to every child entity's table. Calling it twice
    /// is harmless.
    /// </summary>
    public AggregateFeatureBuilder<TAggregateRoot> Audited()
    {
        Features = Features with { Audited = true };
        return this;
    }

    /// <summary>
    /// Adds created/modified stamps like <see cref="Audited()"/>, with <paramref name="configure"/> choosing
    /// the child entities that are left out.
    /// </summary>
    public AggregateFeatureBuilder<TAggregateRoot> Audited(Action<AuditFeatureBuilder<TAggregateRoot>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        Audited();
        configure(new AuditFeatureBuilder<TAggregateRoot>(AuditExceptions));
        return this;
    }
}

/// <summary>The options of <see cref="AggregateFeatureBuilder{TAggregateRoot}.Audited(Action{AuditFeatureBuilder{TAggregateRoot}})"/>.</summary>
public sealed class AuditFeatureBuilder<TAggregateRoot>
    where TAggregateRoot : class, IAggregateRoot
{
    private readonly HashSet<Type> exceptions;

    internal AuditFeatureBuilder(HashSet<Type> exceptions)
    {
        this.exceptions = exceptions;
    }

    /// <summary>
    /// Leaves <typeparamref name="TChild"/>'s table without audit stamps. Only that type is excepted: its own
    /// child entities still take the root's features. <typeparamref name="TChild"/> must be a child entity of
    /// this aggregate, which the EF model build checks; another aggregate root is refused here, since it never
    /// takes this aggregate's features in the first place.
    /// </summary>
    public AuditFeatureBuilder<TAggregateRoot> ExceptChild<TChild>()
        where TChild : class, IEntity
    {
        if (typeof(IAggregateRoot).IsAssignableFrom(typeof(TChild)))
        {
            throw new InvalidOperationException(
                $"The aggregate feature configuration of {typeof(TAggregateRoot).FullName} excepts {typeof(TChild).FullName} "
                + "from Audited(), but that is an aggregate root: another aggregate never takes this one's features, "
                + "so there is nothing to except. Configure it in its own IAggregateFeatureConfiguration<T>.");
        }

        exceptions.Add(typeof(TChild));
        return this;
    }
}
