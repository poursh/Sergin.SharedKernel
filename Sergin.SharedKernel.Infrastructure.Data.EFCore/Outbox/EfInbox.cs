using Microsoft.EntityFrameworkCore;
using Sergin.SharedKernel.Application;
using Sergin.SharedKernel.Application.Events.Integration;
using Sergin.SharedKernel.Application.Times;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Outbox;

/// <summary>
/// The EF-backed <see cref="IInbox{TUnitOfWork}"/>: checks the composite <c>(messageId, handler)</c> key and
/// adds a row when it is missing, but never calls <see cref="IUnitOfWork.SaveChangesAsync"/> itself — the
/// caller's own save, inside the same handler and on the same <typeparamref name="TContext"/>, is what
/// commits the inbox row and the handler's side effect together.
/// </summary>
internal sealed class EfInbox<TContext, TUnitOfWork>(TContext context, IDateTimeProvider clock) : IInbox<TUnitOfWork>
    where TContext : DbContext, IOutboxDbContext
    where TUnitOfWork : IUnitOfWork
{
    public async Task<bool> TryRecordAsync(Guid messageId, string handler, CancellationToken cancellationToken)
    {
        if (await context.InboxMessages.AnyAsync(x => x.MessageId == messageId && x.Handler == handler, cancellationToken))
        {
            return false;
        }

        context.InboxMessages.Add(InboxMessage.Create(messageId, handler, clock.UtcNow));
        return true;
    }
}
