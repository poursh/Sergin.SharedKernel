namespace Sergin.SharedKernel.Application.Events.Integration;

/// <summary>
/// The transport seam of the outbox: the one method a host replaces to move messages between processes. The
/// relay claims a row, wraps it as an <see cref="IntegrationEventEnvelope"/> and calls this; what happens
/// next is the transport's business. The default, <c>InProcessIntegrationEventDispatcher</c>
/// (<c>Sergin.SharedKernel.Infrastructure</c>), moves the message in-process — it deserializes the envelope
/// and publishes it to this host's handlers through MediatR. A broker-backed implementation publishes the
/// envelope's content and headers to a topic instead, and a consumer service on another host rebuilds the
/// envelope from those headers and calls the in-process dispatcher as its last mile. Either way an exception
/// thrown here is what the relay records as a failed attempt. Declared as its own contract, rather than the
/// relay depending on MediatR's <c>IPublisher</c> directly, because the relay lives in
/// <c>Sergin.SharedKernel.Infrastructure.Data.EFCore</c>, which has no MediatR reference and must keep none.
/// </summary>
public interface IIntegrationEventDispatcher
{
    Task DispatchAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken);
}
