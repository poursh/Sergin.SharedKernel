namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Outbox;

/// <summary>
/// One integration event queued for delivery: written by <c>OutboxWriter</c> in the same
/// <c>SaveChangesAsync</c> as the domain change that caused it, and later claimed, dispatched and stamped by
/// the relay (a later task). <see cref="Id"/> is the message's own identity — distinct from the integration
/// event's own <c>Id</c> — because a consumer's inbox dedups on this row's identity, not the event's, and
/// because one domain event can, in principle, be translated into more than one row. It is not an aggregate
/// root and raises no domain events of its own, so adding one to a save never re-triggers the domain-event
/// dispatch loop above it.
/// </summary>
public sealed class OutboxMessage
{
    public const int MaxErrorLength = 4000;

    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    public DateTime OccurredOnUtc { get; private set; }

    public string CorrelationId { get; private set; } = string.Empty;

    public Guid? CausationId { get; private set; }

    public int Attempts { get; private set; }

    public DateTime? NextAttemptAt { get; private set; }

    public DateTime? ProcessedOnUtc { get; private set; }

    public string? Error { get; private set; }

    public static OutboxMessage Create(string type, string content, DateTime occurredOnUtc, string correlationId, Guid? causationId)
        => new()
        {
            Id = Guid.CreateVersion7(),
            Type = type,
            Content = content,
            OccurredOnUtc = occurredOnUtc,
            CorrelationId = correlationId,
            CausationId = causationId
        };

    public void MarkProcessed(DateTime nowUtc)
    {
        ProcessedOnUtc = nowUtc;
        Error = null;
    }

    /// <summary>
    /// Records a failed delivery attempt: increments <see cref="Attempts"/>, keeps only as much of
    /// <paramref name="error"/> as the column allows, and schedules the next attempt after
    /// <paramref name="backoff"/> instead of retrying immediately.
    /// </summary>
    public void MarkFailed(DateTime nowUtc, string error, TimeSpan backoff)
    {
        Attempts++;
        Error = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
        NextAttemptAt = nowUtc + backoff;
    }
}
