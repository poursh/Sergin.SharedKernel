using Sergin.SharedKernel.Application.Commands.Configuration;

namespace Sergin.SharedKernel.Application.Tasks;

/// <summary>
/// The two timeouts of one send of a task. The inactivity timeout is armed when the send starts and re-armed by
/// <see cref="ResetInactivity"/>, which every <see cref="ITaskProgress"/> call makes; <c>MaxDuration</c>, when the task
/// declares one, is armed once and nothing resets it. <see cref="Token"/> is cancelled by whichever expires first, and
/// <see cref="InactivityExpired"/>/<see cref="MaxDurationExpired"/> tell which one did: at most one of them is ever true.
/// An expiry is final: a later reset does not revive the send. Both run on <see cref="CancellationTokenSource.CancelAfter(TimeSpan)"/>,
/// so they follow the real time, never the host's clock. The executor's heartbeat never touches them.
/// </summary>
internal sealed class TaskTimeouts : IDisposable
{
    private const int NoneExpired = 0;
    private const int Inactivity = 1;
    private const int Cap = 2;

    private readonly Lock gate = new();
    private readonly CancellationTokenSource inactivity = new();
    private readonly CancellationTokenSource? maxDuration;
    private readonly CancellationTokenSource linked;
    private readonly CancellationTokenRegistration inactivityExpiry;
    private readonly CancellationTokenRegistration maxDurationExpiry;
    private int expired = NoneExpired;
    private bool disposed;

    /// <summary>Arms both timeouts of <paramref name="settings"/> from now.</summary>
    public TaskTimeouts(TaskSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        InactivityTimeout = settings.InactivityTimeout;
        MaxDuration = settings.MaxDuration;

        if (MaxDuration is not null)
        {
            maxDuration = new CancellationTokenSource();
            linked = CancellationTokenSource.CreateLinkedTokenSource(inactivity.Token, maxDuration.Token);
            maxDurationExpiry = maxDuration.Token.Register(() => Record(Cap));
        }
        else
        {
            linked = CancellationTokenSource.CreateLinkedTokenSource(inactivity.Token);
        }

        inactivityExpiry = inactivity.Token.Register(() => Record(Inactivity));

        if (maxDuration is not null && MaxDuration is { } cap)
        {
            maxDuration.CancelAfter(cap);
        }

        inactivity.CancelAfter(InactivityTimeout);
    }

    /// <summary>The time without progress after which the send is stopped.</summary>
    public TimeSpan InactivityTimeout { get; }

    /// <summary>The longest the send may run however it reports, or null for no cap.</summary>
    public TimeSpan? MaxDuration { get; }

    /// <summary>Cancelled once either timeout expired.</summary>
    public CancellationToken Token => linked.Token;

    /// <summary>The inactivity timeout expired (first).</summary>
    public bool InactivityExpired => Expired() == Inactivity;

    /// <summary><see cref="MaxDuration"/> expired (first).</summary>
    public bool MaxDurationExpired => Expired() == Cap;

    /// <summary>
    /// Re-arms the inactivity timeout from now. Does nothing once either timeout expired (an expiry is final) or once the
    /// send ended, so a report from work the handler left running behind it is harmless.
    /// </summary>
    public void ResetInactivity()
    {
        lock (gate)
        {
            if (disposed || Expired() != NoneExpired)
            {
                return;
            }

            inactivity.CancelAfter(InactivityTimeout);
        }
    }

    // The first expiry, recorded by its source's callback. A source is marked cancelled before its callbacks run, so one
    // that expired while its callback has not run yet is read from the sources themselves.
    private int Expired()
    {
        int first = Volatile.Read(ref expired);

        if (first != NoneExpired)
        {
            return first;
        }

        if (inactivity.IsCancellationRequested)
        {
            return Inactivity;
        }

        return maxDuration?.IsCancellationRequested == true ? Cap : NoneExpired;
    }

    private void Record(int which) => Interlocked.CompareExchange(ref expired, which, NoneExpired);

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
        }

        inactivityExpiry.Dispose();
        maxDurationExpiry.Dispose();
        linked.Dispose();
        maxDuration?.Dispose();
        inactivity.Dispose();
    }
}
