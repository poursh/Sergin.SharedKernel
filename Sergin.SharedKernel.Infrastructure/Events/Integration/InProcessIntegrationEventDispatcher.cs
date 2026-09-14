using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Sergin.SharedKernel.Application.Events.Integration;
using Sergin.SharedKernel.Application.Securities.Users;

namespace Sergin.SharedKernel.Infrastructure.Events.Integration;

/// <summary>
/// The default transport: the message stays in this process. Each envelope is delivered in a consumer scope
/// of its own, opened from the root provider the same way <c>ScopedSerginDispatcher</c> opens a send's scope,
/// and seeded the same way — the relay's identity through <see cref="UserContextAccessor"/>, so a consumer's
/// <c>ISender.Send</c> passes the permission check, and the envelope's correlation and its own message id
/// through <see cref="IntegrationEventContextAccessor"/>, so any outbox row a consumer causes names this
/// message as its cause. The content is then deserialized and published to every registered
/// <c>IIntegrationEventHandler&lt;TEvent&gt;</c> through MediatR's <see cref="IPublisher"/>, resolved from
/// that scope — zero or many handlers, run sequentially, stopping at the first exception, the same shape as
/// <c>DefaultEventDispatcher</c> one layer down. The consumer scope's <c>DbContext</c> is a different
/// instance from the relay's, and a consumer's own save is a separate transaction by design.
/// <para>
/// This is also the last mile every consumer host needs, whatever carried the message to it: a broker
/// consumer service on another host rebuilds the envelope from the message's headers and body and calls
/// this same class — which is why it is <c>public</c> and registered by its concrete type as well as behind
/// <see cref="IIntegrationEventDispatcher"/>. It lives here rather than next to the relay because the relay's
/// project, <c>Sergin.SharedKernel.Infrastructure.Data.EFCore</c>, has no MediatR reference; this is the one
/// project allowed to depend on both.
/// </para>
/// </summary>
public sealed class InProcessIntegrationEventDispatcher(
    IServiceScopeFactory scopeFactory,
    IIntegrationEventSerializer serializer,
    IOutboxRelayIdentity identity) : IIntegrationEventDispatcher
{
    public async Task DispatchAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        using IServiceScope consumerScope = scopeFactory.CreateScope();
        IServiceProvider services = consumerScope.ServiceProvider;

        services.GetRequiredService<UserContextAccessor>().Current = identity.User;
        IntegrationEventContextAccessor eventContext = services.GetRequiredService<IntegrationEventContextAccessor>();
        eventContext.CorrelationId = envelope.CorrelationId;
        eventContext.CausationMessageId = envelope.MessageId;

        IIntegrationEvent integrationEvent = serializer.Deserialize(envelope.Type, envelope.Content);

        await services.GetRequiredService<IPublisher>()
            .Publish(IntegrationEventNotification.Wrap(envelope.MessageId, envelope.CorrelationId, integrationEvent), cancellationToken);
    }
}
