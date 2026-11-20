using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Application.Tasks.Activity;
using Sergin.SharedKernel.Application.Tasks.Commands;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Domain.Tasks;

namespace Sergin.SharedKernel.Application.Tasks;

/// <summary>
/// How a command sent as a run item tells how far it got, in percent. Scoped: a handler injects it like any service.
/// Outside a run item (sent inline, or from a page) a report does nothing, so a handler reports unconditionally.
/// </summary>
public interface ITaskProgress
{
    /// <summary>
    /// Reports <paramref name="percent"/>, clamped to 0–100, and resets the task's inactivity timeout, every call, the
    /// ones whose write the throttle holds back included. Written at once the first time, then at most once per
    /// <see cref="TaskProgress.WriteInterval"/>; the last value reported is always kept on the finished item. A failed
    /// write is logged, never thrown: progress is a courtesy, not part of the command's outcome.
    /// </summary>
    ValueTask ReportAsync(int percent, CancellationToken ct = default);

    /// <summary>
    /// Resets the task's inactivity timeout without a percent, for a handler that cannot measure how far it got. Writes
    /// nothing. Neither call revives a task whose timeout already expired, and neither moves its <c>MaxDuration</c>.
    /// </summary>
    ValueTask KeepAliveAsync(CancellationToken ct = default);
}

/// <summary>
/// The <see cref="ITaskProgress"/> of one send. Every call resets the send's inactivity timeout, through the
/// <see cref="TaskTimeouts"/> the executor put on <see cref="RunItemContext"/>. Each write opens its own scope and store,
/// since the command's own context may be busy (or mid-change) when it reports, then publishes <see cref="RunChanged"/> for the run's user. The value the
/// throttle held back is read by the run-item executor from <see cref="Last"/> when the send ends, and put on the item
/// before the outcome's save.
/// </summary>
internal sealed partial class TaskProgress(
    RunItemContext runItem,
    IServiceScopeFactory scopes,
    IDateTimeProvider clock,
    IRunNotifier notifier,
    ILogger<TaskProgress> logger) : ITaskProgress
{
    /// <summary>The least time between two writes of one item's progress.</summary>
    public static readonly TimeSpan WriteInterval = TimeSpan.FromSeconds(2);

    private readonly Lock gate = new();
    private DateTime? lastWriteAt;

    /// <summary>
    /// The last value reported in this send, clamped; null when nothing was reported. Read by the executor once the send
    /// has ended, when no report is in flight.
    /// </summary>
    public int? Last { get; private set; }

    public async ValueTask ReportAsync(int percent, CancellationToken ct = default)
    {
        if (runItem is not { RunItemId: { } runItemId, Schema: { } schema, Run: var (runId, actingUser) })
        {
            return;
        }

        // Before the throttle: a report it holds back still proves the task is moving.
        runItem.Timeouts?.ResetInactivity();

        int value = Math.Clamp(percent, 0, TaskRunItem.MaxProgress);
        DateTime now = clock.UtcNow;

        lock (gate)
        {
            Last = value;

            if (lastWriteAt is { } previous && now - previous < WriteInterval)
            {
                return;
            }

            lastWriteAt = now;
        }

        try
        {
            await using (AsyncServiceScope scope = scopes.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<ITaskStoreResolver>().For(schema)
                    .SetRunItemProgressAsync(runItemId, value, now, ct);
            }

            notifier.Publish(new RunChanged(new TaskRunRef(schema, runId), actingUser));
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            // The next report writes at once rather than waiting out the interval of a write that never landed.
            lock (gate)
            {
                lastWriteAt = null;
            }

            LogWriteFailed(exception, runItemId);
        }
    }

    public ValueTask KeepAliveAsync(CancellationToken ct = default)
    {
        runItem.Timeouts?.ResetInactivity();
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The progress of scheduled run item {RunItemId} could not be written; the next report tries again.")]
    private partial void LogWriteFailed(Exception exception, Guid runItemId);
}
