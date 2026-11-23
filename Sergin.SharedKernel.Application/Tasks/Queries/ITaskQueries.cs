using Sergin.SharedKernel.Application.Commands.Queries;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Application.Tasks.Activity;
using Sergin.SharedKernel.Domain.Tasks;

namespace Sergin.SharedKernel.Application.Tasks.Queries;

/// <summary>
/// The task reads, raw SQL over the module's schema named on each call. Implemented where the connection
/// factory is (the EF Core project); every method refuses a schema no module opted into, and every read of a task is
/// narrowed to the live rows inside the caller's reach for the request's permission.
/// </summary>
public interface ITaskQueries
{
    Task<ErrorOr<IReadOnlyCollection<TaskScheduleListItem>>> ForTargetAsync(string schema, Guid targetId, CancellationToken ct);

    /// <summary>The schedules of the collection task <paramref name="taskName"/> saved in the scope <paramref name="scopeId"/>.</summary>
    Task<ErrorOr<IReadOnlyCollection<TaskScheduleListItem>>> ForCollectionAsync(string schema, string taskName, Guid scopeId, CancellationToken ct);

    Task<ErrorOr<Versioned<TaskScheduleResponse>>> GetByIdAsync(string schema, TaskScheduleId id, CancellationToken ct);

    Task<ErrorOr<ListQueryResponse<TaskRunListItem>>> RunsAsync(string schema, TaskScheduleId scheduleId, Paggination paggination, CancellationToken ct);

    Task<ErrorOr<IReadOnlyCollection<TaskRunItemListItem>>> RunItemsAsync(string schema, Guid runId, CancellationToken ct);

    Task<ErrorOr<IReadOnlyCollection<TaskRunListItem>>> RecentRunsForTargetAsync(string schema, Guid targetId, int take, CancellationToken ct);

    /// <summary>The latest runs, of every trigger, of the collection task <paramref name="taskName"/> in the scope <paramref name="scopeId"/>.</summary>
    Task<ErrorOr<IReadOnlyCollection<TaskRunListItem>>> RecentRunsForCollectionAsync(string schema, string taskName, Guid scopeId, int take, CancellationToken ct);

    /// <summary>
    /// The runs <paramref name="userId"/> started (Background, Run now, Run once; never a schedule's), running or
    /// finished since <paramref name="finishedSinceUtc"/>, newest first, at most <paramref name="take"/>. The one read
    /// not narrowed by the scope filter: a person's own runs stay theirs after their reach changes.
    /// </summary>
    Task<ErrorOr<IReadOnlyList<ActivityItem>>> ActivityForUserAsync(string schema, Guid userId, DateTime finishedSinceUtc, int take, CancellationToken ct);
}
