using Sergin.SharedKernel.Domain.Users;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Activity;

/// <summary>
/// Tells a user's open circuits, in this process, that one of their runs changed. A signal only: a subscriber re-reads
/// the run, so a missed one costs a late refresh, never a wrong figure.
/// </summary>
public interface IRunNotifier
{
    /// <summary>
    /// Starts every handler subscribed for the change's user and returns; a handler that throws or faults is logged and
    /// never reaches the caller. Publish only after the save the change reports has committed.
    /// </summary>
    void Publish(RunChanged change);

    /// <summary>Hears the changes of <paramref name="user"/>'s runs only, until the result is disposed.</summary>
    IDisposable Subscribe(UserId user, Func<RunChanged, Task> handler);
}
