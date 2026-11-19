using Sergin.SharedKernel.Application.Commands.Queries;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Application.ScheduledTasks.Activity;
using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Queries;

/// <summary>
/// The scheduled-task reads, raw SQL over the module's schema named on each call. Implemented where the connection
/// factory is (the EF Core project); every method refuses a schema no module opted into, and every read of a task is
/// narrowed to the live rows inside the caller's reach for the request's permission.
/// </summary>
public interface IScheduledTaskQueries
{
    Task<ErrorOr<IReadOnlyCollection<ScheduledTaskListItem>>> ForTargetAsync(string schema, Guid targetId, CancellationToken ct);

    Task<ErrorOr<Versioned<ScheduledTaskResponse>>> GetByIdAsync(string schema, ScheduledTaskId id, CancellationToken ct);

    Task<ErrorOr<ListQueryResponse<ScheduledRunListItem>>> RunsAsync(string schema, ScheduledTaskId taskId, Paggination paggination, CancellationToken ct);

    Task<ErrorOr<IReadOnlyCollection<ScheduledRunItemListItem>>> RunItemsAsync(string schema, Guid runId, CancellationToken ct);

    Task<ErrorOr<IReadOnlyCollection<ScheduledRunListItem>>> OneOffRunsForTargetAsync(string schema, Guid targetId, int take, CancellationToken ct);

    /// <summary>
    /// The runs <paramref name="userId"/> started (Background, Run now, Run once; never a schedule's), running or
    /// finished since <paramref name="finishedSinceUtc"/>, newest first, at most <paramref name="take"/>. The one read
    /// not narrowed by the scope filter: a person's own runs stay theirs after their reach changes.
    /// </summary>
    Task<ErrorOr<IReadOnlyList<ActivityItem>>> ActivityForUserAsync(string schema, Guid userId, DateTime finishedSinceUtc, int take, CancellationToken ct);
}
