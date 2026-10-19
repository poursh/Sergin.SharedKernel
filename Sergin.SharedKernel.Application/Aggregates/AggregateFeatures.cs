namespace Sergin.SharedKernel.Application.Aggregates;

/// <summary>The features one entity type has switched on. Grows a flag per feature.</summary>
public sealed record AggregateFeatures(bool Audited, bool SoftDeletable, bool Versioned, bool Scoped = false)
{
    public static AggregateFeatures None { get; } = new(Audited: false, SoftDeletable: false, Versioned: false);
}
