using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Sergin.SharedKernel.Application.Events;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Outbox;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Interceptors;

/// <summary>
/// Dispatches every tracked aggregate's raised domain events from inside <c>SaveChangesAsync</c>, before EF
/// opens the transaction and generates any SQL. Handlers therefore run in the originating scope, on the same
/// <see cref="DbContext"/>: whatever they add is saved with the aggregate change, and an exception aborts the
/// save with nothing written. The loop re-checks the tracker after each round, since a handler may raise
/// further events or add aggregates that raise their own.
/// <para>
/// This used to hook <c>SavedChangesAsync</c> — after commit — which made a failing handler surface an
/// exception for a change that was already durable, and lost every event on a crash between commit and
/// dispatch.
/// </para>
/// <para>
/// Also writes one outbox row per registered translator result for each domain event here, before handlers
/// run, so the row rides the same save as the aggregate change that caused it; an <see cref="OutboxMessage"/>
/// is not an aggregate root and raises nothing, so the loop above still terminates.
/// </para>
/// </summary>
internal sealed class EventDispatcherInterceptor(IEventDispatcher eventDispatcher, IOutboxWriter outboxWriter) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            while (true)
            {
                IReadOnlyCollection<IAggregateRoot> roots = RootsWithPendingEvents(eventData.Context);

                if (roots.Count == 0)
                {
                    break;
                }

                IReadOnlyCollection<IDomainEvent> domainEvents = [.. roots.SelectMany(root => root.DomainEvents)];

                // Clear before dispatching, so an event a handler raises on one of these roots is picked up by
                // the next round rather than re-dispatched alongside the ones already sent.
                foreach (IAggregateRoot root in roots)
                {
                    root.ClearDomainEvents();
                }

                outboxWriter.Write(eventData.Context, domainEvents);

                await eventDispatcher.DispatchAllAsync(domainEvents, cancellationToken);
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// The dispatcher is async-only. Silently saving without dispatching is exactly the event loss this
    /// interceptor exists to prevent, so the synchronous path refuses when there is anything to dispatch.
    /// </summary>
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            IReadOnlyCollection<IAggregateRoot> roots = RootsWithPendingEvents(eventData.Context);

            if (roots.Count > 0)
            {
                string aggregates = string.Join(", ", roots.Select(root => root.GetType().Name).Distinct());

                throw new InvalidOperationException(
                    $"Aggregate(s) {aggregates} have pending domain events, which are dispatched only from "
                    + "SaveChangesAsync. Use SaveChangesAsync instead of SaveChanges.");
            }
        }

        return base.SavingChanges(eventData, result);
    }

    private static IReadOnlyCollection<IAggregateRoot> RootsWithPendingEvents(DbContext context) =>
    [
        .. context.ChangeTracker.Entries<IAggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(root => root.DomainEvents.Count > 0)
    ];
}
