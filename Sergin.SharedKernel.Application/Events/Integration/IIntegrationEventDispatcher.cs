namespace Sergin.SharedKernel.Application.Events.Integration;

/// <summary>
/// What the relay calls to deliver a claimed message to its handlers. Declared as its own contract, rather
/// than the relay depending on MediatR's <c>IPublisher</c> directly, because the relay lives in
/// <c>Sergin.SharedKernel.Infrastructure.Data.EFCore</c>, which has no MediatR reference; the implementation
/// that actually wraps <c>IPublisher</c> lives in <c>Sergin.SharedKernel.Infrastructure</c> instead, the one
/// project allowed to depend on both.
/// </summary>
public interface IIntegrationEventDispatcher
{
    Task DispatchAsync(Guid messageId, string correlationId, IIntegrationEvent integrationEvent, CancellationToken cancellationToken);
}
