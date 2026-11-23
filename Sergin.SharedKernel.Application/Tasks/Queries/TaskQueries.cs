using Sergin.SharedKernel.Application.Commands.Queries;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Domain.Tasks;

namespace Sergin.SharedKernel.Application.Tasks.Queries;

/// <summary>The schedules that target one record, in the module's schema.</summary>
public sealed record GetTaskSchedulesForTargetQuery(string Schema, Guid TargetId) : IQuery<IReadOnlyCollection<TaskScheduleListItem>>;

/// <summary>The schedules of one collection task (<c>ITaskCommand&lt;TRoot&gt;</c>), by its name, saved in one scope.</summary>
public sealed record GetTaskSchedulesForCollectionQuery(string Schema, string TaskName, Guid ScopeId) : IQuery<IReadOnlyCollection<TaskScheduleListItem>>;

/// <summary>One schedule with its targets; its row version travels in <c>ConcurrencyContext.Current</c>.</summary>
public sealed record GetTaskScheduleByIdQuery(string Schema, TaskScheduleId Id) : IQuery<TaskScheduleResponse>;

/// <summary>A schedule's runs, newest first.</summary>
public sealed record GetTaskScheduleRunsQuery(string Schema, TaskScheduleId ScheduleId, Paggination Paggination) : IQuery<ListQueryResponse<TaskRunListItem>>;

public sealed record GetTaskRunItemsQuery(string Schema, Guid RunId) : IQuery<IReadOnlyCollection<TaskRunItemListItem>>;

/// <summary>The latest runs over one record, whatever started them (a schedule, Run now, Run once, a person).</summary>
public sealed record GetRecentRunsForTargetQuery(string Schema, Guid TargetId, int Take) : IQuery<IReadOnlyCollection<TaskRunListItem>>;

/// <summary>The latest runs of one collection task, by its name, in one scope, whatever started them.</summary>
public sealed record GetRecentRunsForCollectionQuery(string Schema, string TaskName, Guid ScopeId, int Take) : IQuery<IReadOnlyCollection<TaskRunListItem>>;

internal sealed class GetTaskSchedulesForTargetQueryHandler(ITaskQueries queries)
    : IQueryHandler<GetTaskSchedulesForTargetQuery, IReadOnlyCollection<TaskScheduleListItem>>
{
    public Task<ErrorOr<IReadOnlyCollection<TaskScheduleListItem>>> Handle(GetTaskSchedulesForTargetQuery request, CancellationToken cancellationToken) =>
        queries.ForTargetAsync(request.Schema, request.TargetId, cancellationToken);
}

internal sealed class GetTaskSchedulesForCollectionQueryHandler(ITaskQueries queries)
    : IQueryHandler<GetTaskSchedulesForCollectionQuery, IReadOnlyCollection<TaskScheduleListItem>>
{
    public Task<ErrorOr<IReadOnlyCollection<TaskScheduleListItem>>> Handle(GetTaskSchedulesForCollectionQuery request, CancellationToken cancellationToken) =>
        queries.ForCollectionAsync(request.Schema, request.TaskName, request.ScopeId, cancellationToken);
}

internal sealed class GetTaskScheduleByIdQueryHandler(ITaskQueries queries, ConcurrencyContext concurrency)
    : VersionedQueryHandler<GetTaskScheduleByIdQuery, TaskScheduleResponse>(concurrency)
{
    public override Task<ErrorOr<Versioned<TaskScheduleResponse>>> HandleVersioned(GetTaskScheduleByIdQuery request, CancellationToken cancellationToken) =>
        queries.GetByIdAsync(request.Schema, request.Id, cancellationToken);
}

internal sealed class GetTaskScheduleRunsQueryHandler(ITaskQueries queries)
    : IQueryHandler<GetTaskScheduleRunsQuery, ListQueryResponse<TaskRunListItem>>
{
    public Task<ErrorOr<ListQueryResponse<TaskRunListItem>>> Handle(GetTaskScheduleRunsQuery request, CancellationToken cancellationToken) =>
        queries.RunsAsync(request.Schema, request.ScheduleId, request.Paggination, cancellationToken);
}

internal sealed class GetTaskRunItemsQueryHandler(ITaskQueries queries)
    : IQueryHandler<GetTaskRunItemsQuery, IReadOnlyCollection<TaskRunItemListItem>>
{
    public Task<ErrorOr<IReadOnlyCollection<TaskRunItemListItem>>> Handle(GetTaskRunItemsQuery request, CancellationToken cancellationToken) =>
        queries.RunItemsAsync(request.Schema, request.RunId, cancellationToken);
}

internal sealed class GetRecentRunsForTargetQueryHandler(ITaskQueries queries)
    : IQueryHandler<GetRecentRunsForTargetQuery, IReadOnlyCollection<TaskRunListItem>>
{
    public Task<ErrorOr<IReadOnlyCollection<TaskRunListItem>>> Handle(GetRecentRunsForTargetQuery request, CancellationToken cancellationToken) =>
        queries.RecentRunsForTargetAsync(request.Schema, request.TargetId, request.Take, cancellationToken);
}

internal sealed class GetRecentRunsForCollectionQueryHandler(ITaskQueries queries)
    : IQueryHandler<GetRecentRunsForCollectionQuery, IReadOnlyCollection<TaskRunListItem>>
{
    public Task<ErrorOr<IReadOnlyCollection<TaskRunListItem>>> Handle(GetRecentRunsForCollectionQuery request, CancellationToken cancellationToken) =>
        queries.RecentRunsForCollectionAsync(request.Schema, request.TaskName, request.ScopeId, request.Take, cancellationToken);
}
