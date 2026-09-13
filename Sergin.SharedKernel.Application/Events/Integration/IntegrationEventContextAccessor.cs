namespace Sergin.SharedKernel.Application.Events.Integration;

/// <summary>
/// Carries the correlation and causation of the message currently being consumed into a DI scope that has no
/// other way to know it — the same role <see cref="Securities.Users.UserContextAccessor"/> plays for the
/// caller's identity. The relay seeds both properties on the consumer scope before dispatching a message; if
/// the resulting handler causes a new domain event, the outbox writer reads them back so the new outbox
/// message carries the same correlation id and names the consumed message as its cause.
/// </summary>
/// <remarks>
/// Registered scoped: one instance per scope, seeded at most once by the relay, read at most once by the
/// writer within that same scope.
/// </remarks>
public sealed class IntegrationEventContextAccessor
{
    public string? CorrelationId { get; set; }

    public Guid? CausationMessageId { get; set; }
}
