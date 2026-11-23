using System.Text.Json;
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
    public const string Aggregate = "aggregate";

    public static ListFieldSet Set { get; } = ListFieldSet.Create(
        new ListField(StartedAt, ListFieldKind.Instant, Sortable: true, new HashSet<FilteringType> { FilteringType.between }),
        new ListField(Task, ListFieldKind.Text, Sortable: true, new HashSet<FilteringType> { FilteringType.equals }),
        ListField.FilterOnly(Trigger, ListFieldKind.Text, FilteringType.equals),
        ListField.FilterOnly(Status, ListFieldKind.Text, FilteringType.equals),
        ListField.FilterOnly(StartedBy, ListFieldKind.Identifier, FilteringType.equals),
        ListField.FilterOnly(Aggregate, ListFieldKind.Text, FilteringType.equals));
}

internal sealed class GetTaskRunsQueryHandler(ITaskQueries queries, TaskCatalog catalog) : IListQueryHandler<GetTaskRunsQuery, TaskRunsListItem>
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<ErrorOr<ListQueryResponse<TaskRunsListItem>>> Handle(GetTaskRunsQuery request, CancellationToken cancellationToken)
    {
        FilterData? aggregate = request.Filtering?.Filters.FirstOrDefault(filter => filter.Id == TaskRunListFields.Aggregate);

        if (aggregate is null)
        {
            return await queries.RunsListAsync(request.Schema, request, taskNames: null, cancellationToken);
        }

        // The aggregate is not a column: it stands for the tasks the catalogue holds on that root in this module.
        string? value = aggregate.Value?.ToString();
        string[] names =
        [
            .. catalog.Commands
                .Where(command => command.Schema == request.Schema && TaskNames.RootKey(command.Root) == value)
                .Select(command => command.Name),
        ];

        if (names.Length == 0)
        {
            return Error.Validation(nameof(request.Filtering), $"'{value}' is not an aggregate with tasks in this module.");
        }

        var remaining = request.Filtering!.Filters
            .Where(filter => filter.Id != TaskRunListFields.Aggregate)
            .Select(filter => new { filter.Id, filter.Value, filter.Mode })
            .ToArray();

        GetTaskRunsQuery narrowed = request with
        {
            Filtering = remaining.Length == 0 ? null : Filtering.Create(JsonSerializer.Serialize(remaining, SerializerOptions)),
        };

        return await queries.RunsListAsync(request.Schema, narrowed, names, cancellationToken);
    }
}

/// <summary>The people who started the runs the caller can reach, for the run list's Started by filter.</summary>
public sealed record GetTaskRunActorsQuery(string Schema) : IQuery<IReadOnlyCollection<Guid>>;

internal sealed class GetTaskRunActorsQueryHandler(ITaskQueries queries) : IQueryHandler<GetTaskRunActorsQuery, IReadOnlyCollection<Guid>>
{
    public Task<ErrorOr<IReadOnlyCollection<Guid>>> Handle(GetTaskRunActorsQuery request, CancellationToken cancellationToken) =>
        queries.RunActorsAsync(request.Schema, cancellationToken);
}

/// <summary>Refuses a sort or filter the run list does not declare in <see cref="TaskRunListFields"/>.</summary>
internal sealed class GetTaskRunsQueryValidator() : ListQueryValidator<GetTaskRunsQuery>(TaskRunListFields.Set);
