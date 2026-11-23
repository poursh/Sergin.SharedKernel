using System.Threading.Channels;
using Sergin.SharedKernel.Application.Tasks;

namespace Sergin.SharedKernel.Infrastructure.Tasks.TickerQ;

/// <summary>
/// The engine a host runs with no TickerQ licence (<c>Sergin:Tasks:TickerQ:LicensePath</c> blank or absent): run
/// items are written to an in-process channel that <see cref="InProcessRunItemWorker"/> executes, so run-now and one-off
/// runs work, retries included; a saved schedule is kept and never fires (<see cref="FiresTriggers"/> is false). It holds
/// no trigger at all, so once the real engine replaces it the start-up rebuild recreates one for every saved schedule.
/// Items still in the channel are lost on restart; the run item recovery hands them over again after StaleAfter.
/// </summary>
public sealed class InProcessTaskEngine : ITaskEngine
{
    private readonly Channel<InProcessRunItem> items = Channel.CreateUnbounded<InProcessRunItem>(new UnboundedChannelOptions { SingleReader = true });

    public bool FiresTriggers => false;

    /// <summary>What the worker reads.</summary>
    internal ChannelReader<InProcessRunItem> Items => items.Reader;

    /// <summary>A new id the task links to; nothing is registered and nothing fires.</summary>
    public Task<Guid> ScheduleAsync(TaskScheduleTrigger trigger, CancellationToken ct) => Task.FromResult(Guid.CreateVersion7());

    public Task RescheduleAsync(Guid engineTriggerId, TaskScheduleTrigger trigger, CancellationToken ct) => Task.CompletedTask;

    public Task SetEnabledAsync(Guid engineTriggerId, bool enabled, CancellationToken ct) => Task.CompletedTask;

    public Task RemoveAsync(Guid engineTriggerId, CancellationToken ct) => Task.CompletedTask;

    public Task<bool> ExistsAsync(Guid engineTriggerId, CancellationToken ct) => Task.FromResult(false);

    public Task<bool?> GetEnabledAsync(Guid engineTriggerId, CancellationToken ct) => Task.FromResult<bool?>(null);

    public Task<IReadOnlyCollection<TaskEngineTrigger>> ListTriggersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyCollection<TaskEngineTrigger>>([]);

    public Task<Guid> EnqueueRunItemAsync(TaskRunItemReference item, CancellationToken ct) => EnqueueRunItemAsync(item, null, ct);

    public bool IsValidCron(string cron, string timeZoneId) => CronSchedule.IsValid(cron, timeZoneId);

    public IReadOnlyList<DateTimeOffset> NextOccurrences(string cron, string timeZoneId, DateTimeOffset after, int count) =>
        CronSchedule.NextOccurrences(cron, timeZoneId, after, count);

    /// <summary>A run item's job, due at <paramref name="dueUtc"/> or, when null, now.</summary>
    internal Task<Guid> EnqueueRunItemAsync(TaskRunItemReference item, DateTime? dueUtc, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);
        ct.ThrowIfCancellationRequested();

        // Unbounded and never completed: a write cannot wait or fail, so this only guards the impossible.
        if (!items.Writer.TryWrite(new InProcessRunItem(item, dueUtc)))
        {
            throw new InvalidOperationException($"The in-process engine refused run item {item.RunItemId}.");
        }

        return Task.FromResult(Guid.CreateVersion7());
    }
}

/// <summary>A run item in the stand-in's channel, and the earliest time it may run (null: now).</summary>
internal sealed record InProcessRunItem(TaskRunItemReference Item, DateTime? DueUtc);
