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
}

public sealed class AggregateFeatureBuilder<TEntity> : AggregateFeatureBuilder
    where TEntity : class, IEntity
{
    internal AggregateFeatureBuilder()
    {
    }

    /// <summary>Adds created/modified stamps to the entity's table. Calling it twice is harmless.</summary>
    public AggregateFeatureBuilder<TEntity> Audited()
    {
        Features = Features with { Audited = true };
        return this;
    }
}
