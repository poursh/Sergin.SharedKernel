using Microsoft.Extensions.Logging;

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
            Task task = subscription.Invoke(change);

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

    /// <summary>
    /// One handler, with the execution context it was subscribed in. A publisher is a worker thread (a run item, the
    /// recovery sweep) running in the host's default culture; a subscriber is a Blazor circuit running in the viewer's.
    /// Calling the handler on the publisher's context made the circuit's InvokeAsync — which runs inline when the circuit
    /// is idle — render the page in the host's culture on every publish, so the handler runs in the subscriber's context,
    /// as a timer callback or a <c>Task.Run</c> continuation does.
    /// </summary>
    private sealed class Subscription(RunNotifier owner, UserId user, Func<RunChanged, Task> handler) : IDisposable
    {
        private readonly ExecutionContext? context = ExecutionContext.Capture();
        private volatile bool disposed;

        public UserId User { get; } = user;

        public bool IsDisposed => disposed;

        public Task Invoke(RunChanged change)
        {
            if (context is null)
            {
                // Flow was suppressed when subscribing: there is no context to restore.
                return handler(change);
            }

            Task task = Task.CompletedTask;
            ExecutionContext.Run(context, _ => task = handler(change), null);
            return task;
        }

        public void Dispose()
        {
            disposed = true;
            owner.Remove(this);
        }
    }
}
