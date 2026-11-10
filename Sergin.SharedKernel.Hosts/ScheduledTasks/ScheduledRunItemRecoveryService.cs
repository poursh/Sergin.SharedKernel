using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.ScheduledTasks;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.ScheduledTasks;

namespace Sergin.SharedKernel.Hosts.ScheduledTasks;

/// <summary>
/// The one recovery for run items, whatever lost their job: every <see cref="ScheduledTaskOptions.RecoveryInterval"/>,
/// each opted-in module's unfinished items quiet for longer than <see cref="ScheduledTaskOptions.StaleAfter"/> are handed
/// to the engine again and linked to the new job. That covers an item saved but never enqueued, a retry whose job was
/// never made, a delivery cut off at shutdown or by a database error with the item left Running, and a job the engine
/// gave up on. An unfinished item is never quiet that long otherwise: every claim, retry and hand-over stamps its activity,
/// and the options refuse a stale period shorter than the longest retry wait plus one interval.
/// <para>
/// A duplicate job is harmless: the executor claims the item in the database before it sends, so a second delivery of a
/// healthy item does nothing, and a Running item is taken over only once it has been quiet for the stale period. An item
/// whose every allowed attempt was claimed and lost ends Failed as exhausted, and its run completes.
/// </para>
/// <para>Public, like <c>OutboxRelayService</c>, so an integration test can remove it and run a pass by hand.</para>
/// </summary>
public sealed partial class ScheduledRunItemRecoveryService(
    IServiceProvider services,
    IEnumerable<ScheduledTasksRegistration> registrations,
    IDateTimeProvider clock,
    IOptions<ScheduledTaskOptions> options,
    ILogger<ScheduledRunItemRecoveryService> logger) : BackgroundService
{
    private readonly string[] schemas = [.. registrations.Select(registration => registration.Schema).Distinct(StringComparer.Ordinal)];

    /// <summary>One pass over every opted-in schema; answers how many items were handed over again.</summary>
    public async Task<int> RecoverOnceAsync(CancellationToken ct)
    {
        int recovered = 0;

        if (schemas.Length == 0)
        {
            return recovered;
        }

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IScheduledTaskEngine engine = scope.ServiceProvider.GetRequiredService<IScheduledTaskEngine>();
        IScheduledTaskStoreResolver stores = scope.ServiceProvider.GetRequiredService<IScheduledTaskStoreResolver>();
        DateTime quietSince = clock.UtcNow - options.Value.StaleAfter;

        foreach (string schema in schemas)
        {
            try
            {
                IScheduledTaskStore store = stores.For(schema);

                foreach (Guid itemId in await store.ListStaleRunItemsAsync(quietSince, ct))
                {
                    Guid job = await engine.EnqueueRunItemAsync(new ScheduledRunItemReference(schema, itemId), ct);

                    if (await store.RelinkStaleRunItemAsync(itemId, job, clock.UtcNow, quietSince, ct))
                    {
                        recovered++;
                        LogRecovered(itemId, schema);
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogRecoveryFailed(exception, schema);
            }
        }

        return recovered;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (schemas.Length == 0)
        {
            return;
        }

        TimeSpan interval = options.Value.RecoveryInterval;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(interval, stoppingToken);
                await RecoverOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown: the loop ends by throwing out of its delay or its pass, which is the expected exit.
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scheduled run item {RunItemId} in schema {Schema} had gone quiet; it was handed to the job engine again.")]
    private partial void LogRecovered(Guid runItemId, string schema);

    [LoggerMessage(Level = LogLevel.Error, Message = "The scheduled run item recovery failed for schema {Schema}; retrying after the recovery interval.")]
    private partial void LogRecoveryFailed(Exception exception, string schema);
}
