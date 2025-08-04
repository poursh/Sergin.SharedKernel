using Microsoft.EntityFrameworkCore.Diagnostics;
using Sergin.SharedKernel.Application.Events;
using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Interceptors;

internal sealed class EventDispatcherInterceptor(IEventDispatcher eventDispatcher) : SaveChangesInterceptor
{
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            IEnumerable<IAggregateRoot> entities = eventData.Context.ChangeTracker.Entries<IAggregateRoot>()
                .Where(e => e.Entity.DomainEvents.Any())
                .Select(e => e.Entity);

            IEnumerable<IDomainEvent> domainEvents = entities.SelectMany(e => e.DomainEvents);

            await eventDispatcher.DispatchAllAsync(domainEvents, cancellationToken);
            
            foreach (IAggregateRoot root in entities)
            { 
                root.ClearDomainEvents();
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
