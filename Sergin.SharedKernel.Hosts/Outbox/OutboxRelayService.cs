using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.Events.Integration;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Outbox;

namespace Sergin.SharedKernel.Hosts.Outbox;

/// <summary>
/// The host's outbox pump. For every registered <see cref="IOutboxRelaySource"/> — one per module whose
/// <c>DbContext</c> opted into the outbox — it runs a relay loop and a purge loop for the life of the host.
/// The relay loop calls <see cref="IOutboxRelaySource.RelayOnceAsync"/>; a full batch means there is more
/// waiting and the next pass follows at once, a short one means the source is drained and the loop sleeps
/// <see cref="OutboxOptions.PollInterval"/>. A pass that throws — a database outage, a schema that is not
/// there yet — is logged and the loop carries on after the same delay, so transient trouble never kills
/// the relay; a consumer that throws never reaches here, because the source stamps that row failed and
/// moves on. The purge loop calls <see cref="IOutboxRelaySource.PurgeAsync"/> every
/// <see cref="OutboxOptions.PurgeInterval"/>. Cancellation on shutdown ends every loop quietly; a relay
/// transaction interrupted mid-pass disposes without committing and its rows are re-claimed next start.
/// <para>
/// Public, unlike the rest of the relay, so an integration test can find its <c>IHostedService</c>
/// registration and remove it when it wants to drive a source by hand.
/// </para>
/// </summary>
public sealed class OutboxRelayService(
    IEnumerable<IOutboxRelaySource> sources,
    IServiceProvider services,
    IOptions<OutboxOptions> options,
    ILogger<OutboxRelayService> logger) : BackgroundService
{
    /// <summary>
    /// Resolving the registry builds its name-to-type map, which throws on a missing or duplicate
    /// <c>[IntegrationEventName]</c>. Doing it here, before the loops start, turns that into a host-start
    /// failure rather than a warning logged from the first pass — and does so even when no module has opted
    /// into the outbox yet, when nothing else on the start path would touch the registry.
    /// </summary>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _ = services.GetRequiredService<IIntegrationEventTypeRegistry>();
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        OutboxOptions settings = options.Value;
        List<Task> loops = [];

        foreach (IOutboxRelaySource source in sources)
        {
            string schema = source.Schema;

            // Guarded because CA1873 treats an Information-level call's params array as a cost worth skipping.
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Outbox relay started for schema {Schema}.", schema);
            }

            loops.Add(RelayLoopAsync(source, schema, settings, stoppingToken));
            loops.Add(PurgeLoopAsync(source, schema, settings, stoppingToken));
        }

        try
        {
            await Task.WhenAll(loops);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown: every loop ends by throwing out of its delay or its pass, which is the expected exit.
        }
    }

    private async Task RelayLoopAsync(IOutboxRelaySource source, string schema, OutboxOptions settings, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            int claimed = 0;

            try
            {
                claimed = await source.RelayOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Outbox relay pass failed for schema {Schema}; retrying after the poll interval.", schema);
            }

            if (claimed < settings.BatchSize)
            {
                await Task.Delay(settings.PollInterval, stoppingToken);
            }
        }
    }

    private async Task PurgeLoopAsync(IOutboxRelaySource source, string schema, OutboxOptions settings, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(settings.PurgeInterval, stoppingToken);

            try
            {
                await source.PurgeAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Outbox purge failed for schema {Schema}; retrying after the purge interval.", schema);
            }
        }
    }
}
