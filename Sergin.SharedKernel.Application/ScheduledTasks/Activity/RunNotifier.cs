using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Domain.Users;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Activity;

/// <summary>The in-process <see cref="IRunNotifier"/>: a lock-guarded list of subscriptions per user.</summary>
internal sealed partial class RunNotifier(ILogger<RunNotifier> logger) : IRunNotifier
{
    private readonly Lock gate = new();
    private readonly Dictionary<UserId, List<Subscription>> byUser = [];

    public void Publish(RunChanged change)
    {
        if (change is null)
        {
            return;
        }

        Subscription[] listeners;

        lock (gate)
        {
            listeners = byUser.TryGetValue(change.ActingUser, out List<Subscription>? found) ? [.. found] : [];
        }

        foreach (Subscription subscription in listeners)
        {
            Deliver(subscription, change);
        }
    }

    public IDisposable Subscribe(UserId user, Func<RunChanged, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(handler);

        var subscription = new Subscription(this, user, handler);

        lock (gate)
        {
            if (!byUser.TryGetValue(user, out List<Subscription>? list))
            {
                byUser[user] = list = [];
            }

            list.Add(subscription);
        }

        return subscription;
    }

    private void Deliver(Subscription subscription, RunChanged change)
    {
        // A subscription disposed after the snapshot was taken is not called.
        if (subscription.IsDisposed)
        {
            return;
        }

        try
        {
            Task task = subscription.Handler(change);

            if (!task.IsCompleted)
            {
                _ = task.ContinueWith(
                    faulted => LogHandlerFaulted(faulted.Exception!, change.Ref.RunId),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            else if (task.IsFaulted)
            {
                LogHandlerFaulted(task.Exception, change.Ref.RunId);
            }
        }
#pragma warning disable CA1031 // a subscriber's failure must never reach the starter or the executor
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogHandlerFaulted(exception, change.Ref.RunId);
        }
    }

    private void Remove(Subscription subscription)
    {
        lock (gate)
        {
            if (byUser.TryGetValue(subscription.User, out List<Subscription>? list) && list.Remove(subscription) && list.Count == 0)
            {
                byUser.Remove(subscription.User);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A subscriber to the changes of run {RunId} failed; the run is not affected.")]
    private partial void LogHandlerFaulted(Exception exception, Guid runId);

    private sealed class Subscription(RunNotifier owner, UserId user, Func<RunChanged, Task> handler) : IDisposable
    {
        private volatile bool disposed;

        public UserId User { get; } = user;

        public Func<RunChanged, Task> Handler { get; } = handler;

        public bool IsDisposed => disposed;

        public void Dispose()
        {
            disposed = true;
            owner.Remove(this);
        }
    }
}
