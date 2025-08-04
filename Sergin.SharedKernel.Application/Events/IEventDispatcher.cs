using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Application.Events;

public interface IEventDispatcher
{
    Task DispatchAsync(IDomainEvent @event, CancellationToken cancellationToken = default);
    async Task DispatchAllAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (IDomainEvent @event in events)
        {
            await DispatchAsync(@event, cancellationToken);
        }
    }
}
