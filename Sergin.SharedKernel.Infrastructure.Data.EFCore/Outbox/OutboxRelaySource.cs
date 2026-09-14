using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.Events.Integration;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Application.Times;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Outbox;

/// <summary>
/// The relay for one module's outbox: claims a batch of pending rows, delivers each to its consumers, and
/// stamps the outcome — all inside one transaction on a relay-owned <typeparamref name="TContext"/>. The
/// claim is <c>FOR UPDATE SKIP LOCKED</c>, so the rows stay locked until that transaction commits: two relay
/// instances never process the same row concurrently, but a crash after delivery and before commit leaves
/// the row unstamped and it is redelivered on the next pass — at-least-once by construction, which is what
/// <see cref="InboxIntegrationEventHandler{TEvent, TUnitOfWork}"/> exists to absorb. A row whose delivery
/// throws is stamped with the attempt and a backoff and skipped, and later rows of the same aggregate overtake
/// it: there is no head-of-line blocking, so consumers must not assume in-order delivery. A
/// <see cref="DbUpdateException"/> from a consumer whose inbox insert lost a race against another relay
/// instance is just a failed attempt — the retry finds the inbox row and the handler skips.
/// <para>
/// Each row is delivered in a consumer scope of its own, opened from the root provider the same way
/// <c>ScopedSerginDispatcher</c> opens a send's scope, and seeded the same way: the relay's identity through
/// <see cref="UserContextAccessor"/>, so a consumer's <c>ISender.Send</c> passes the permission check, and the
/// row's correlation and causation through <see cref="IntegrationEventContextAccessor"/>, so any outbox row
/// a consumer causes names this one. The consumer scope's <c>DbContext</c> is a different instance from the
/// relay's, and a consumer's own save is a separate transaction by design.
/// </para>
/// </summary>
internal sealed class OutboxRelaySource<TContext>(
    string schema,
    IServiceScopeFactory scopeFactory,
    IIntegrationEventSerializer serializer,
    IDateTimeProvider clock,
    IOptions<OutboxOptions> options,
    IOutboxRelayIdentity identity,
    ILogger<OutboxRelaySource<TContext>> logger) : IOutboxRelaySource
    where TContext : DbContext, IOutboxDbContext
{
    private const int MaxBackoffSeconds = 300;

    // Built once. Identifiers are constants ("outbox_messages" is what ApplyOutbox maps); values are {n}
    // parameters. Passing a prebuilt string, not an interpolated literal, to FromSqlRaw keeps EF1002 quiet.
    private readonly string claimSql =
        "SELECT * FROM \"" + schema + "\".\"outbox_messages\" "
        + "WHERE processed_on_utc IS NULL AND attempts < {1} AND (next_attempt_at IS NULL OR next_attempt_at <= {0}) "
        + "ORDER BY id LIMIT {2} FOR UPDATE SKIP LOCKED";

    public string Schema => schema;

    public async Task<int> RelayOnceAsync(CancellationToken cancellationToken)
    {
        OutboxOptions settings = options.Value;
        using IServiceScope relayScope = scopeFactory.CreateScope();
        TContext context = relayScope.ServiceProvider.GetRequiredService<TContext>();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // List rather than IReadOnlyCollection: CA1859 insists on the concrete type ToListAsync already returns.
        List<OutboxMessage> batch = await context.OutboxMessages
            .FromSqlRaw(claimSql, clock.UtcNow, settings.MaxAttempts, settings.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (OutboxMessage message in batch)
        {
            try
            {
                await DeliverAsync(message, cancellationToken);
                message.MarkProcessed(clock.UtcNow);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                int attempts = message.Attempts + 1;
                message.MarkFailed(clock.UtcNow, exception.ToString(), Backoff(attempts));
                logger.LogWarning(
                    exception,
                    "Outbox message {MessageId} ({Type}) in schema {Schema} failed on attempt {Attempt}.",
                    message.Id,
                    message.Type,
                    schema,
                    attempts);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return batch.Count;
    }

    public async Task PurgeAsync(CancellationToken cancellationToken)
    {
        DateTime cutoff = clock.UtcNow - options.Value.Retention;
        using IServiceScope scope = scopeFactory.CreateScope();
        TContext context = scope.ServiceProvider.GetRequiredService<TContext>();

        await context.OutboxMessages
            .Where(message => message.ProcessedOnUtc != null && message.ProcessedOnUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
        await context.InboxMessages
            .Where(message => message.ProcessedOnUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// An <see cref="OperationCanceledException"/> raised here during shutdown is deliberately not caught by
    /// the caller: the relay transaction disposes without committing, and the rows are re-claimed next start.
    /// </summary>
    private async Task DeliverAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        using IServiceScope consumerScope = scopeFactory.CreateScope();
        IServiceProvider services = consumerScope.ServiceProvider;

        services.GetRequiredService<UserContextAccessor>().Current = identity.User;
        IntegrationEventContextAccessor eventContext = services.GetRequiredService<IntegrationEventContextAccessor>();
        eventContext.CorrelationId = message.CorrelationId;
        eventContext.CausationMessageId = message.Id;

        IIntegrationEvent integrationEvent = serializer.Deserialize(message.Type, message.Content);

        await services.GetRequiredService<IIntegrationEventDispatcher>()
            .DispatchAsync(message.Id, message.CorrelationId, integrationEvent, cancellationToken);
    }

    private static TimeSpan Backoff(int attempts) =>
        TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempts), MaxBackoffSeconds));
}
