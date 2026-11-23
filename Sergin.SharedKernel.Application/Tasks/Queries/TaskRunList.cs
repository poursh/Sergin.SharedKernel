using Sergin.SharedKernel.Application.Commands.Queries;
using Sergin.SharedKernel.Application.Validations;

namespace Sergin.SharedKernel.Application.Tasks.Queries;

/// <summary>
/// Every run of one module's tasks, whatever started it, newest first unless sorted otherwise: the Task runs page's
/// list. Only the runs inside the caller's reach for the manage permission are listed and counted.
/// </summary>
public sealed record GetTaskRunsQuery : ListQuery<TaskRunsListItem>
{
    public GetTaskRunsQuery(
        string schema,
        Paggination paggination,
        Term? term = default,
        Filtering? filtering = default,
        Sorting? sorting = default)
        : base(paggination, term, filtering, sorting)
    {
        Schema = schema;
    }

    /// <summary>The schema of the module whose runs are listed.</summary>
    public string Schema { get; }
}

/// <summary>
/// What the run list may be sorted and filtered by. Every filter is single-valued; <see cref="Status"/> is the shown
/// <see cref="RunOutcome"/>, by its member name.
/// </summary>
public static class TaskRunListFields
{
    public const string StartedAt = "startedAt";
    public const string Task = "task";
    public const string Trigger = "trigger";
    public const string Status = "status";
    public const string StartedBy = "startedBy";

    public static ListFieldSet Set { get; } = ListFieldSet.Create(
        new ListField(StartedAt, ListFieldKind.Instant, Sortable: true, new HashSet<FilteringType> { FilteringType.between }),
        new ListField(Task, ListFieldKind.Text, Sortable: true, new HashSet<FilteringType> { FilteringType.equals }),
        ListField.FilterOnly(Trigger, ListFieldKind.Text, FilteringType.equals),
        ListField.FilterOnly(Status, ListFieldKind.Text, FilteringType.equals),
        ListField.FilterOnly(StartedBy, ListFieldKind.Identifier, FilteringType.equals));
}

internal sealed class GetTaskRunsQueryHandler(ITaskQueries queries) : IListQueryHandler<GetTaskRunsQuery, TaskRunsListItem>
{
    public Task<ErrorOr<ListQueryResponse<TaskRunsListItem>>> Handle(GetTaskRunsQuery request, CancellationToken cancellationToken) =>
        queries.RunsListAsync(request.Schema, request, cancellationToken);
}

/// <summary>Refuses a sort or filter the run list does not declare in <see cref="TaskRunListFields"/>.</summary>
internal sealed class GetTaskRunsQueryValidator() : ListQueryValidator<GetTaskRunsQuery>(TaskRunListFields.Set);
