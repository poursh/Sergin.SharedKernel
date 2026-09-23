namespace Sergin.SharedKernel.Application.Aggregates;

/// <summary>
/// Declares which platform features apply to one aggregate, the way an EF
/// <c>IEntityTypeConfiguration&lt;T&gt;</c> declares its mapping. A module writes one class per configured
/// aggregate root in its <c>.Application</c> project, named <c>&lt;Root&gt;AggregateFeatureConfiguration</c> so it
/// cannot collide with the EF <c>&lt;Type&gt;Configuration</c>.
/// <para>
/// Constrained to <see cref="IAggregateRoot"/>, because a feature is a decision about the whole aggregate:
/// every child entity takes the root's features by default (the EF convention walks the root's navigations),
/// and a feature's own builder is where a child is excepted from it, e.g.
/// <c>builder.Audited(audit =&gt; audit.ExceptChild&lt;DeviceModel&gt;())</c>.
/// </para>
/// <para>
/// A configuration is a declaration, not a service: it is created with <c>Activator</c>, never through
/// DI, so it must have a parameterless constructor.
/// </para>
/// </summary>
public interface IAggregateFeatureConfiguration<TAggregateRoot>
    where TAggregateRoot : class, IAggregateRoot
{
    void Configure(AggregateFeatureBuilder<TAggregateRoot> builder);
}
