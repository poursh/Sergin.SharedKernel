namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>
/// Declares the platform policy of one request — which permissions its caller needs, whether it must carry
/// an expected version — the way <see cref="Aggregates.IAggregateFeatureConfiguration{TAggregateRoot}"/>
/// declares an aggregate's features. A module writes one <c>internal sealed class &lt;RecordName&gt;Configuration</c>
/// next to the record, in its <c>.Application.Contracts</c> project, so a gateway hosting the module Remote sees
/// it too. A request without a configuration has no policy.
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
