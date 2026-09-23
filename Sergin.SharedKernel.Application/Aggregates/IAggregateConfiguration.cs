namespace Sergin.SharedKernel.Application.Aggregates;

/// <summary>
/// Declares which platform features apply to one entity type, the way an EF
/// <c>IEntityTypeConfiguration&lt;T&gt;</c> declares its mapping. A module writes one class per configured
/// type in its <c>.Application</c> project, named <c>&lt;Type&gt;AggregateConfiguration</c> so it cannot collide
/// with the EF <c>&lt;Type&gt;Configuration</c>. Constrained to <see cref="IEntity"/>, not
/// <see cref="IAggregateRoot"/>, so a child entity can be configured on its own.
/// <para>
/// A configuration is a declaration, not a service: it is created with <c>Activator</c>, never through
/// DI, so it must have a parameterless constructor.
/// </para>
/// </summary>
public interface IAggregateConfiguration<TEntity>
    where TEntity : class, IEntity
{
    void Configure(AggregateFeatureBuilder<TEntity> builder);
}
