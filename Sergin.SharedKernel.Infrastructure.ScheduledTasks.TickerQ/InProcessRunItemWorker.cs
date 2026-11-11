using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Application.Times;

namespace Sergin.SharedKernel.Infrastructure.ScheduledTasks.TickerQ;

/// <summary>
/// Executes the stand-in's run items: each in a fresh scope through <see cref="RunItemDelivery"/>, the delivery the TickerQ
/// function makes, at most <see cref="Environment.ProcessorCount"/> at a time. A retry waits out its interval outside the
/// limit, then takes a slot. Every exception is logged and the loop goes on: the executor already records an attempt's
/// failure on the item, and the recovery picks up whatever a failed delivery left unfinished.
/// </summary>
internal sealed partial class InProcessRunItemWorker(
    InProcessScheduledTaskEngine engine,
    IServiceScopeFactory scopes,
    IDateTimeProvider clock,
    ILogger<InProcessRunItemWorker> logger) : BackgroundService
{
    private readonly SemaphoreSlim slots = new(Environment.ProcessorCount);
    private readonly ConcurrentDictionary<Task, byte> deliveries = new();

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        LogEngineOff();

        return base.StartAsync(cancellationToken);
    }

    public override void Dispose()
    {
        slots.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (InProcessRunItem next in engine.Items.ReadAllAsync(stoppingToken))
            {
                Track(DeliverAsync(next, stoppingToken));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown: the read ends by throwing, which is the expected exit.
        }
        finally
        {
            // Each delivery catches its own failures, so this only waits for them to see the cancellation.
            await Task.WhenAll(deliveries.Keys);
        }
    }

    private void Track(Task delivery)
    {
        deliveries.TryAdd(delivery, 0);
        _ = delivery.ContinueWith(
            done => deliveries.TryRemove(done, out _), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private async Task DeliverAsync(InProcessRunItem next, CancellationToken ct)
    {
        try
        {
            if (next.DueUtc is { } due && due - clock.UtcNow is { Ticks: > 0 } wait)
            {
                await Task.Delay(wait, ct);
            }

            await slots.WaitAsync(ct);

            try
            {
                await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                await RunItemDelivery.DeliverAsync(
                    scope.ServiceProvider, next.Item, (item, dueUtc, token) => engine.EnqueueRunItemAsync(item, dueUtc, token), ct);
            }
            finally
            {
                slots.Release();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown mid-delivery: the item stays unfinished and the recovery hands it over after the restart.
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            LogDeliveryFailed(exception, next.Item.RunItemId, next.Item.Schema);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "TickerQ licence not configured; scheduled runs are off, run-now and one-off runs execute in process.")]
    private partial void LogEngineOff();

    [LoggerMessage(Level = LogLevel.Error, Message = "The in-process engine failed to deliver scheduled run item {RunItemId} in schema {Schema}; the recovery will hand it over again.")]
    private partial void LogDeliveryFailed(Exception exception, Guid runItemId, string schema);
}
