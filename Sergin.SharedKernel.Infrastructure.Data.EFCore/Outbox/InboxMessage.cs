namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Outbox;

/// <summary>
/// Records that a given outbox message has already been handled by a given consumer handler, keyed on both
/// together so the same message can be independently tracked by more than one handler. Written by
/// <c>EfInbox{TContext, TUnitOfWork}</c> on the same <c>DbContext</c> the handler's own side effect saves
/// through, so the record and the effect commit — or roll back — together, rather than racing each other
/// across two separate saves.
/// </summary>
public sealed class InboxMessage
{
    private InboxMessage()
    {
    }

    public Guid MessageId { get; private set; }

    public string Handler { get; private set; } = string.Empty;

    public DateTime ProcessedOnUtc { get; private set; }

    public static InboxMessage Create(Guid messageId, string handler, DateTime nowUtc)
        => new()
        {
            MessageId = messageId,
            Handler = handler,
            ProcessedOnUtc = nowUtc
        };
}
