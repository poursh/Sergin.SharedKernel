using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sergin.SharedKernel.Application.Events.Integration;
using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Outbox;

/// <summary>
/// Resolves <c>IIntegrationEventTranslator&lt;TDomainEvent&gt;</c> for each domain event's runtime type from
/// the current scope, and adds one <see cref="OutboxMessage"/> per translator result. A domain event with no
/// registered translator produces no row — most domain events never leave their module. Translator discovery
/// goes through <see cref="IServiceProvider"/> rather than MediatR because this project has no MediatR
/// reference; the closed generic type per domain event type is cached, since it never changes for a given
/// runtime type and reflection to build it is otherwise repeated on every save.
/// </summary>
internal sealed class OutboxWriter(
    IServiceProvider services,
    IIntegrationEventTypeRegistry registry,
    IIntegrationEventSerializer serializer,
    IntegrationEventContextAccessor eventContext) : IOutboxWriter
{
    private static readonly ConcurrentDictionary<Type, Type> translatorTypes = new();

    public void Write(DbContext context, IReadOnlyCollection<IDomainEvent> domainEvents)
    {
        foreach (IDomainEvent domainEvent in domainEvents)
        {
            Type closed = translatorTypes.GetOrAdd(
                domainEvent.GetType(),
                static t => typeof(IIntegrationEventTranslator<>).MakeGenericType(t));

            IReadOnlyCollection<IIntegrationEventTranslator> translators =
                [.. services.GetServices(closed).OfType<IIntegrationEventTranslator>()];

            if (translators.Count == 0)
            {
                continue;
            }

            if (context is not IOutboxDbContext outbox)
            {
                throw new InvalidOperationException(
                    $"{context.GetType().Name} has a translator registered for {domainEvent.GetType().Name} but "
                    + "does not implement IOutboxDbContext. Implement it and call modelBuilder.ApplyOutbox() in "
                    + "OnModelCreating.");
            }

            string correlationId = eventContext.CorrelationId
                ?? Activity.Current?.TraceId.ToString()
                ?? Guid.CreateVersion7().ToString();

            foreach (IIntegrationEventTranslator translator in translators)
            {
                IIntegrationEvent integrationEvent = translator.Translate(domainEvent);

                outbox.OutboxMessages.Add(OutboxMessage.Create(
                    registry.NameOf(integrationEvent.GetType()),
                    serializer.Serialize(integrationEvent),
                    integrationEvent.OccurredOnUtc,
                    correlationId,
                    eventContext.CausationMessageId));
            }
        }
    }
}
