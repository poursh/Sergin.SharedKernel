namespace Sergin.SharedKernel.Application.ScheduledTasks.Activity;

/// <summary>A user's own runs across every module's store, newest first; a store that cannot be read is left out.</summary>
public interface IBackgroundRunDirectory
{
    Task<IReadOnlyList<ActivityItem>> ListActivityAsync(UserId user, DateTime finishedSinceUtc, CancellationToken ct);
}
