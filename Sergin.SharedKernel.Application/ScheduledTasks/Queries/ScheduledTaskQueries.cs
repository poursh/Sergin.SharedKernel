using Sergin.SharedKernel.Application.Commands.Queries;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Queries;

/// <summary>The schedules that target one record, in the module's schema.</summary>
public sealed record GetScheduledTasksForTargetQuery(string Schema, Guid TargetId) : IQuery<IReadOnlyCollection<ScheduledTaskListItem>>;

/// <summary>One schedule with its targets; its row version travels in <c>ConcurrencyContext.Current</c>.</summary>
public sealed record GetScheduledTaskByIdQuery(string Schema, ScheduledTaskId Id) : IQuery<ScheduledTaskResponse>;

/// <summary>A schedule's runs, newest first.</summary>
public sealed record GetScheduledTaskRunsQuery(string Schema, ScheduledTaskId TaskId, Paggination Paggination) : IQuery<ListQueryResponse<ScheduledRunListItem>>;

public sealed record GetScheduledTaskRunItemsQuery(string Schema, Guid RunId) : IQuery<IReadOnlyCollection<ScheduledRunItemListItem>>;

/// <summary>The latest one-off runs (no schedule) over one record.</summary>
public sealed record GetOneOffRunsForTargetQuery(string Schema, Guid TargetId, int Take) : IQuery<IReadOnlyCollection<ScheduledRunListItem>>;

internal sealed class GetScheduledTasksForTargetQueryHandler(IScheduledTaskQueries queries)
    : IQueryHandler<GetScheduledTasksForTargetQuery, IReadOnlyCollection<ScheduledTaskListItem>>
{
    public Task<ErrorOr<IReadOnlyCollection<ScheduledTaskListItem>>> Handle(GetScheduledTasksForTargetQuery request, CancellationToken cancellationToken) =>
        queries.ForTargetAsync(request.Schema, request.TargetId, cancellationToken);
}

internal sealed class GetScheduledTaskByIdQueryHandler(IScheduledTaskQueries queries, ConcurrencyContext concurrency)
    : VersionedQueryHandler<GetScheduledTaskByIdQuery, ScheduledTaskResponse>(concurrency)
{
    public override Task<ErrorOr<Versioned<ScheduledTaskResponse>>> HandleVersioned(GetScheduledTaskByIdQuery request, CancellationToken cancellationToken) =>
        queries.GetByIdAsync(request.Schema, request.Id, cancellationToken);
}

internal sealed class GetScheduledTaskRunsQueryHandler(IScheduledTaskQueries queries)
    : IQueryHandler<GetScheduledTaskRunsQuery, ListQueryResponse<ScheduledRunListItem>>
{
    public Task<ErrorOr<ListQueryResponse<ScheduledRunListItem>>> Handle(GetScheduledTaskRunsQuery request, CancellationToken cancellationToken) =>
        queries.RunsAsync(request.Schema, request.TaskId, request.Paggination, cancellationToken);
}

internal sealed class GetScheduledTaskRunItemsQueryHandler(IScheduledTaskQueries queries)
    : IQueryHandler<GetScheduledTaskRunItemsQuery, IReadOnlyCollection<ScheduledRunItemListItem>>
{
    public Task<ErrorOr<IReadOnlyCollection<ScheduledRunItemListItem>>> Handle(GetScheduledTaskRunItemsQuery request, CancellationToken cancellationToken) =>
        queries.RunItemsAsync(request.Schema, request.RunId, cancellationToken);
}

internal sealed class GetOneOffRunsForTargetQueryHandler(IScheduledTaskQueries queries)
    : IQueryHandler<GetOneOffRunsForTargetQuery, IReadOnlyCollection<ScheduledRunListItem>>
{
    public Task<ErrorOr<IReadOnlyCollection<ScheduledRunListItem>>> Handle(GetOneOffRunsForTargetQuery request, CancellationToken cancellationToken) =>
        queries.OneOffRunsForTargetAsync(request.Schema, request.TargetId, request.Take, cancellationToken);
}
