namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>
/// Declares the platform policy of one request — which permissions its caller needs, its text category and the
/// properties no person sees — the way <see cref="Aggregates.IAggregateFeatureConfiguration{TAggregateRoot}"/>
/// declares an aggregate's features. A module writes one <c>internal sealed class &lt;RecordName&gt;Configuration</c>
/// in its <c>.Application.Configurations</c> project, mirroring the record's folder, so a gateway hosting the
/// module Remote sees it too. A request without a configuration has no permissions.
/// <para>
/// The aggregate a request works on, where its permissions are checked and whether it must carry an expected version
/// all come from the request's type (its target interface from <c>Commands.Types</c>), never from here: a request on a
/// <c>Scoped()</c> root is checked on the current scope, one on any other root on any scope, and an untyped request on
/// the current scope. A command on an existing entity, child or child collection of a <c>Versioned()</c> root requires
/// an expected version unless this configuration calls <c>WithoutExpectedVersion()</c>.
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
