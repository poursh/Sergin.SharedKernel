using MediatR;
using Sergin.SharedKernel.Application.Events.Integration;

namespace Sergin.SharedKernel.Infrastructure.Events.Integration;

/// <summary>
/// Publishes an integration event to every registered <c>IIntegrationEventHandler&lt;TEvent&gt;</c> through
/// MediatR's <see cref="IPublisher"/> — the same shape as <c>DefaultEventDispatcher</c> one layer down, zero
/// or many handlers, run sequentially, stopping at the first exception. The relay calls this instead of
/// <see cref="IPublisher"/> directly because it lives in
/// <c>Sergin.SharedKernel.Infrastructure.Data.EFCore</c>, which has no MediatR reference.
/// </summary>
internal sealed class DefaultIntegrationEventDispatcher(IPublisher publisher) : IIntegrationEventDispatcher
{
    public Task DispatchAsync(Guid messageId, string correlationId, IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        return publisher.Publish(IntegrationEventNotification.Wrap(messageId, correlationId, integrationEvent), cancellationToken);
    }
}
