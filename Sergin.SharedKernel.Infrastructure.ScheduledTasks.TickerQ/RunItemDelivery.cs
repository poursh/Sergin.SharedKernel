using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.ScheduledTasks;
using Sergin.SharedKernel.Application.Times;

namespace Sergin.SharedKernel.Infrastructure.ScheduledTasks.TickerQ;

/// <summary>
/// One delivery of a run item, the same for both engines: the runner executes it, and an item the runner asks to try
/// again gets a new job, due after Sergin's <see cref="ScheduledTaskOptions.RetryIntervalsSeconds"/> for the attempt just
/// made (the last interval repeats), and is linked to it. Only how a job is made due later is the engine's.
/// </summary>
internal static class RunItemDelivery
{
    /// <param name="services">A fresh scope's provider, the delivery's own.</param>
    /// <param name="item">The item delivered.</param>
    /// <param name="enqueueAt">Makes a job for the item due at the given UTC time; answers the job's id.</param>
    /// <param name="ct">Cancels the delivery.</param>
    public static async Task DeliverAsync(
        IServiceProvider services,
        ScheduledRunItemReference item,
        Func<ScheduledRunItemReference, DateTime, CancellationToken, Task<Guid>> enqueueAt,
        CancellationToken ct)
    {
        if (!await services.GetRequiredService<IScheduledTaskRunner>().ExecuteRunItemAsync(item.Schema, item.RunItemId, ct))
        {
            return;
        }

        IScheduledTaskStore store = services.GetRequiredService<IScheduledTaskStoreResolver>().For(item.Schema);

        // Purged meanwhile: nothing left to retry.
        if (await store.GetRunItemAsync(item.RunItemId, ct) is not { Item: { } retried })
        {
            return;
        }

        IReadOnlyList<int> intervals = services.GetRequiredService<IOptions<ScheduledTaskOptions>>().Value.RetryIntervalsSeconds;
        int seconds = intervals[Math.Clamp(retried.Attempts - 1, 0, intervals.Count - 1)];
        IDateTimeProvider clock = services.GetRequiredService<IDateTimeProvider>();

        // Lost if the host stops before the link is saved: the item then stays Retrying and quiet, and the recovery
        // hands it over again once it has been quiet for StaleAfter.
        Guid job = await enqueueAt(item, clock.UtcNow.AddSeconds(seconds), ct);

        retried.LinkJob(job, clock.UtcNow);
        await store.SaveAsync(ct);
    }
}
