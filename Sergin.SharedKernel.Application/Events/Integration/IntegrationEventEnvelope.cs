namespace Sergin.SharedKernel.Application.Events.Integration;

/// <summary>
/// An outbox row as a transport sees it — what the relay hands to <see cref="IIntegrationEventDispatcher"/>
/// per claimed message. <paramref name="Type"/> is the wire name from <see cref="IntegrationEventNameAttribute"/>
/// and <paramref name="Content"/> is the JSON exactly as stored in <c>outbox_messages.content</c>, so a broker
/// adapter can map the envelope onto a body plus headers (and a routing key) without knowing any event type;
/// only the in-process dispatcher, and a broker consumer's last mile, ever deserialize it.
/// <paramref name="MessageId"/> is the row's own id — what a consumer's inbox dedups on — not the event's.
/// <paramref name="CausationId"/> is carried for log and trace headers only: the consumer side still sets
/// <see cref="IntegrationEventContextAccessor.CausationMessageId"/> to <paramref name="MessageId"/>, because
/// the message being consumed is the cause of whatever a handler writes next, not the message that caused it.
/// </summary>
public sealed record IntegrationEventEnvelope(
    Guid MessageId,
    string Type,
    string Content,
    DateTime OccurredOnUtc,
    string CorrelationId,
    Guid? CausationId);
