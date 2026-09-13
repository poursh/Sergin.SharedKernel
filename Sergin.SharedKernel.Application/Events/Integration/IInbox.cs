namespace Sergin.SharedKernel.Application.Events.Integration;

/// <summary>
/// The consumer-side half of turning at-least-once delivery into an effectively-once side effect: a
/// per-module table keyed on <c>(messageId, handler)</c> that
/// <see cref="InboxIntegrationEventHandler{TEvent, TUnitOfWork}"/> checks before running a handler's side
/// effect, so a message the relay redelivers after a crash, or that two relay instances race on, is applied
/// at most once. Generic over the module's own unit of work so an implementation can add the inbox row on the
/// same <typeparamref name="TUnitOfWork"/> the handler will save through, letting the row and the side effect
/// commit together.
/// </summary>
// TUnitOfWork bounds which module's inbox this is (so DI can register one IInbox<> per module's unit of
// work) without appearing in TryRecordAsync's own signature — the same shape InboxIntegrationEventHandler
// below is generic over. S2326 (unused type parameter) does not apply to that distinguishing role.
#pragma warning disable S2326
public interface IInbox<TUnitOfWork>
    where TUnitOfWork : IUnitOfWork
#pragma warning restore S2326
{
    /// <summary>
    /// Records that <paramref name="handler"/> is about to process <paramref name="messageId"/>. Returns
    /// <see langword="false"/> when that pair was already recorded — the caller must then do nothing, since
    /// the side effect already ran on an earlier delivery.
    /// </summary>
    Task<bool> TryRecordAsync(Guid messageId, string handler, CancellationToken cancellationToken);
}
