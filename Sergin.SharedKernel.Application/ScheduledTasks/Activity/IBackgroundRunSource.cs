namespace Sergin.SharedKernel.Application.ScheduledTasks.Activity;

/// <summary>
/// One module's store of runs, as the activity directory reads it: the user's own runs a person started, running or
/// finished since <c>finishedSinceUtc</c>, newest first, at most <c>take</c>. Own runs are not narrowed by the scope
/// filter: a run stays the user's after their reach changes. Registered scoped, one per opted-in context.
/// </summary>
public interface IBackgroundRunSource
{
    string Schema { get; }

    Task<IReadOnlyList<ActivityItem>> ListActivityAsync(UserId user, DateTime finishedSinceUtc, int take, CancellationToken ct);
}
