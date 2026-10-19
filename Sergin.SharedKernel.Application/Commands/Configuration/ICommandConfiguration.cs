namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>
/// Declares the platform policy of one request — which permissions its caller needs, whether it must carry
/// an expected version — the way <see cref="Aggregates.IAggregateFeatureConfiguration{TAggregateRoot}"/>
/// declares an aggregate's features. A module writes one <c>internal sealed class &lt;RecordName&gt;Configuration</c>
/// in its <c>.Application.Configurations</c> project, mirroring the record's folder, so a gateway hosting the
/// module Remote sees it too. A request without a configuration has no policy.
/// <para>
/// Use this shape for a request with no main aggregate: its permissions are checked on the current scope. A
/// request that works on one aggregate implements
/// <see cref="ICommandConfiguration{TCommand, TAggregateRoot}"/> instead.
/// </para>
/// <para>
/// A configuration is a declaration, not a service: it is created with <c>Activator</c>, never through DI,
/// so it must have a parameterless constructor. <see cref="CommandConfigurationRegistry"/> runs it once.
/// </para>
/// </summary>
public interface ICommandConfiguration<TCommand>
    where TCommand : IBaseCommand
{
    void Configure(CommandConfigurationBuilder<TCommand> builder);
}

/// <summary>
/// Declares the policy of a request that works on <typeparamref name="TAggregateRoot"/>, like
/// <see cref="ICommandConfiguration{TCommand}"/>, and lets the registry derive what the aggregate's feature
/// configuration already says. A root that is <c>Scoped()</c> has the request's permissions checked on the current
/// scope; any other root, one with no feature configuration included, on any scope: its records belong to no
/// scope, so the caller only has to hold the permission somewhere. The configuration cannot choose otherwise.
/// <c>RequireExpectedVersion()</c> is refused unless the root is <c>Versioned()</c>. The feature configuration is read from the same ConfigurationsAssembly.
/// </summary>
// TAggregateRoot is read by the registry through reflection, never by a member: naming the aggregate is its whole
// role, so S2326 (unused type parameter) does not apply.
#pragma warning disable S2326
public interface ICommandConfiguration<TCommand, TAggregateRoot>
    where TCommand : IBaseCommand
    where TAggregateRoot : class, IAggregateRoot
{
    void Configure(CommandConfigurationBuilder<TCommand> builder);
}
#pragma warning restore S2326
