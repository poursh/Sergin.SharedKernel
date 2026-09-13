using Microsoft.EntityFrameworkCore;
using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Outbox;

/// <summary>
/// The producer half of the outbox: turns a batch of domain events into zero or more <see cref="OutboxMessage"/>
/// rows, one per registered translator result per event. Declared here, internal, so
/// <c>EventDispatcherInterceptor</c> can call it without this project taking on a MediatR reference —
/// translator discovery and resolution both happen through the ambient <see cref="IServiceProvider"/>, not
/// MediatR.
/// </summary>
internal interface IOutboxWriter
{
    void Write(DbContext context, IReadOnlyCollection<IDomainEvent> domainEvents);
}
