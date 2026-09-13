namespace Sergin.SharedKernel.Application.Events.Integration;

/// <summary>
/// The shape every integration event must carry. <see cref="Id"/> is the event's own identity, distinct from
/// the outbox message that transports it (that row has its own id — see the outbox design) — so a consumer
/// can tell "the same event happened twice" from "this event was redelivered". <see cref="OccurredOnUtc"/> is
/// the underlying domain change's timestamp, not the time the message was written or delivered.
/// </summary>
public interface IIntegrationEvent
{
    Guid Id { get; }

    DateTime OccurredOnUtc { get; }
}
