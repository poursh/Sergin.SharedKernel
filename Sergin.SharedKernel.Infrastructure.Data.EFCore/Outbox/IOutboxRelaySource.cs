namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Outbox;

/// <summary>
/// One module's outbox as the relay sees it: a schema to drain, one claim-deliver-stamp pass, and a purge.
/// Registered once per <see cref="IOutboxDbContext"/>-bearing module by
/// <c>ModuleDbContextExtensions.AddModuleDbContext</c>, so the host's relay service loops over the collection
/// without knowing any module's context type — and a test picks a source by <see cref="Schema"/> and drives
/// it directly, without the service in between.
/// </summary>
public interface IOutboxRelaySource
{
    /// <summary>
    /// The module schema this source drains, e.g. <c>"dm"</c>. Tests pick a source by it.
    /// </summary>
    string Schema { get; }

    /// <summary>
    /// Claims one batch, delivers each row in its own consumer scope, stamps the outcome, commits. Returns the
    /// number of rows claimed — the caller uses a short batch as its cue to back off and poll instead.
    /// </summary>
    Task<int> RelayOnceAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Deletes processed outbox rows and inbox rows older than <see cref="OutboxOptions.Retention"/>.
    /// </summary>
    Task PurgeAsync(CancellationToken cancellationToken);
}
