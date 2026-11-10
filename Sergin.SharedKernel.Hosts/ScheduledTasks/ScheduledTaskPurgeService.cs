using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.ScheduledTasks;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.ScheduledTasks;

namespace Sergin.SharedKernel.Hosts.ScheduledTasks;

/// <summary>
/// The run history's purge loop, the outbox's purge loop's twin: every <see cref="ScheduledTaskOptions.PurgeInterval"/>
/// each opted-in module's finished runs completed more than <see cref="ScheduledTaskOptions.Retention"/> ago are deleted,
/// with their items by the foreign key's cascade. A run still in progress is kept whatever its age. A pass that throws
/// is logged and the loop carries on after the same interval; cancellation on shutdown ends it quietly.
/// <para>Public, like <c>OutboxRelayService</c>, so an integration test can find its registration and remove it.</para>
/// </summary>
public sealed partial class ScheduledTaskPurgeService(
    IServiceProvider services,
    IEnumerable<ScheduledTasksRegistration> registrations,
    IDateTimeProvider clock,
    IOptions<ScheduledTaskOptions> options,
    ILogger<ScheduledTaskPurgeService> logger) : BackgroundService
{
    private readonly string[] schemas = [.. registrations.Select(registration => registration.Schema).Distinct(StringComparer.Ordinal)];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (schemas.Length == 0)
        {
            return;
        }

        ScheduledTaskOptions settings = options.Value;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(settings.PurgeInterval, stoppingToken);
                await PurgeOnceAsync(settings.Retention, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown: the loop ends by throwing out of its delay or its pass, which is the expected exit.
        }
    }

    private async Task PurgeOnceAsync(TimeSpan retention, CancellationToken ct)
    {
        DateTime cutoff = clock.UtcNow - retention;
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IScheduledTaskStoreResolver stores = scope.ServiceProvider.GetRequiredService<IScheduledTaskStoreResolver>();

        foreach (string schema in schemas)
        {
            try
            {
                int purged = await stores.For(schema).PurgeAsync(cutoff, ct);

                if (purged > 0)
                {
                    LogPurged(purged, schema);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogPurgeFailed(exception, schema);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Purged {Count} finished scheduled run(s) in schema {Schema}.")]
    private partial void LogPurged(int count, string schema);

    [LoggerMessage(Level = LogLevel.Error, Message = "The scheduled-run purge failed for schema {Schema}; retrying after the purge interval.")]
    private partial void LogPurgeFailed(Exception exception, string schema);
}
