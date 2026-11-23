using System.Data.Common;
using Dapper;
using ErrorOr;
using Sergin.SharedKernel.Application;
using Sergin.SharedKernel.Application.Commands.Queries;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Application.Tasks;
using Sergin.SharedKernel.Application.Tasks.Activity;
using Sergin.SharedKernel.Application.Tasks.Commands;
using Sergin.SharedKernel.Application.Tasks.Queries;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Domain.Tasks;
using Sergin.SharedKernel.Infrastracture.Data;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Queries;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Tasks;

/// <summary>
/// The task reads, raw SQL through <see cref="IDbConnectionFactory"/>, one connection per call, in the schema
/// of the module the request names. The schema is a SQL identifier, so it is composed into the text only after it
/// matches a <see cref="TasksRegistration"/>: an unknown one is a validation error and nothing is sent.
/// <para>
/// Raw SQL is not reached by EF's filters, so every read of <c>task_schedules</c> adds <c>deleted_at_utc IS NULL</c>
/// and <see cref="ScopeColumns.ReachSql"/>. A run is reached through its task's scope (a deleted task hides its
/// history), a one-off run, which has no task, through its own scope path. Reach's columns are unqualified, so each
/// predicate sits in a query whose nearest table is the scoped one. The one exception is a user's own activity
/// (<see cref="ActivityForUserAsync"/>), which is narrowed by the acting user instead.
/// </para>
/// </summary>
internal sealed class TaskQueries(
    IDbConnectionFactory connectionFactory,
    ScopeFilter scopeFilter,
    IEnumerable<TasksRegistration> registrations,
    TaskCatalog catalog) : ITaskQueries
{
    private const int MaxRecentTake = 100;

    private const int MaxActivityTake = 100;

    private static readonly Error UnknownSchema = Error.Validation(
        "Tasks.UnknownSchema", "No module opted into scheduled tasks under that schema.");

    public Task<ErrorOr<IReadOnlyCollection<TaskScheduleListItem>>> ForTargetAsync(string schema, Guid targetId, CancellationToken ct) =>
        SchedulesAsync(
            schema,
            quoted => $"EXISTS (SELECT 1 FROM {quoted}.task_schedule_targets y WHERE y.schedule_id = t.id AND y.target_id = @TargetId)",
            new DynamicParameters(new { TargetId = targetId }),
            ct);

    // A collection task's schedules have no targets: they are the task's, saved in the scope.
    public Task<ErrorOr<IReadOnlyCollection<TaskScheduleListItem>>> ForCollectionAsync(string schema, string taskName, Guid scopeId, CancellationToken ct) =>
        SchedulesAsync(
            schema,
            _ => "t.command_name = @TaskName AND t.scope_id = @ScopeId",
            new DynamicParameters(new { TaskName = taskName, ScopeId = scopeId }),
            ct);

    private async Task<ErrorOr<IReadOnlyCollection<TaskScheduleListItem>>> SchedulesAsync(
        string schema, Func<string, string> filterOf, DynamicParameters parameters, CancellationToken ct)
    {
        if (!TryQuote(schema, out string quoted))
        {
            return UnknownSchema;
        }

        using DbConnection connection = await connectionFactory.CreateConnectionAsync();

        // The last run is the task's newest, of any status; the lateral exposes no scope column, so Reach stays unambiguous.
        string sql =
            $$"""
            SELECT t.id AS Id, t.command_name AS CommandName, t.cron AS Cron, t.time_zone_id AS TimeZoneId, t.state AS State,
                   lr.started_at_utc AS LastRunAtUtc, lr.status AS LastRunStatus, lr.failed AS LastRunFailed,
                   (SELECT count(*) FROM {{quoted}}.task_schedule_targets x WHERE x.schedule_id = t.id)::int AS TargetCount
            FROM {{quoted}}.task_schedules t
            LEFT JOIN LATERAL (
                SELECT r.started_at_utc, r.status, r.failed
                FROM {{quoted}}.task_runs r
                WHERE r.schedule_id = t.id
                ORDER BY r.started_at_utc DESC, r.id DESC
                LIMIT 1) lr ON true
            WHERE t.deleted_at_utc IS NULL AND {{ScopeColumns.ReachSql}}
              AND {{filterOf(quoted)}}
            ORDER BY t.id;
            """;

        ScopeColumns.AddParameters(parameters, scopeFilter);

        // The schema was matched against the registered ones, the filter is one of the two above; every value is a parameter.
#pragma warning disable S2077
        IEnumerable<TaskRow> rows = await connection.QueryAsync<TaskRow>(new CommandDefinition(sql, parameters, cancellationToken: ct));
#pragma warning restore S2077

        IReadOnlyCollection<TaskScheduleListItem> items =
        [
            .. rows.Select(row => new TaskScheduleListItem(
                row.Id,
                row.CommandName,
                row.Cron,
                row.TimeZoneId,
                Enum.Parse<TaskScheduleState>(row.State),
                row.LastRunAtUtc,
                row.LastRunStatus is null ? null : Enum.Parse<TaskRunStatus>(row.LastRunStatus),
                row.LastRunFailed,
                row.TargetCount)),
        ];

        return items.ToErrorOr();
    }

    public async Task<ErrorOr<Versioned<TaskScheduleResponse>>> GetByIdAsync(string schema, TaskScheduleId id, CancellationToken ct)
    {
        if (!TryQuote(schema, out string quoted))
        {
            return UnknownSchema;
        }

        using DbConnection connection = await connectionFactory.CreateConnectionAsync();

        string sql =
            $$"""
            SELECT id AS Id, command_name AS CommandName, arguments::text AS Arguments, cron AS Cron,
                   time_zone_id AS TimeZoneId, state AS State, delegating_user_id AS DelegatingUserId,
                   row_version AS RowVersion
            FROM {{quoted}}.task_schedules
            WHERE id = @Id AND deleted_at_utc IS NULL AND {{ScopeColumns.ReachSql}};

            SELECT x.target_id AS TargetId
            FROM {{quoted}}.task_schedule_targets x
            WHERE x.schedule_id = @Id
            ORDER BY x.target_id;
            """;

        DynamicParameters parameters = ScopeColumns.AddParameters(new DynamicParameters(new { Id = id.Value }), scopeFilter);

#pragma warning disable S2077
        SqlMapper.GridReader grid = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
#pragma warning restore S2077

        await using (grid)
        {
            TaskDetailRow? row = await grid.ReadSingleOrDefaultAsync<TaskDetailRow>();

            if (row is null)
            {
                return Error.NotFound();
            }

            IReadOnlyCollection<TaskScheduleTarget> targets =
                [.. (await grid.ReadAsync<TargetRow>()).Select(target => new TaskScheduleTarget(target.TargetId))];

            return new Versioned<TaskScheduleResponse>(
                new TaskScheduleResponse(
                    row.Id,
                    row.CommandName,
                    row.Arguments,
                    row.Cron,
                    row.TimeZoneId,
                    Enum.Parse<TaskScheduleState>(row.State),
                    row.DelegatingUserId,
                    targets),
                RowVersion.Create(row.RowVersion));
        }
    }

    public async Task<ErrorOr<ListQueryResponse<TaskRunListItem>>> RunsAsync(
        string schema, TaskScheduleId scheduleId, Paggination paggination, CancellationToken ct)
    {
        if (!TryQuote(schema, out string quoted))
        {
            return UnknownSchema;
        }

        using DbConnection connection = await connectionFactory.CreateConnectionAsync();

        // The task's reach gates the history: the subquery's nearest table is task_schedules, so Reach binds to it.
        string from =
            $$"""
            FROM {{quoted}}.task_runs r
            WHERE r.schedule_id = @ScheduleId
              AND EXISTS (
                  SELECT 1 FROM {{quoted}}.task_schedules
                  WHERE id = @ScheduleId AND deleted_at_utc IS NULL AND {{ScopeColumns.ReachSql}})
            """;

        string sql =
            $"""
            SELECT count(*) {from};

            SELECT {RunColumns}
            {from}
            ORDER BY r.started_at_utc DESC, r.id DESC
            LIMIT @PageSize OFFSET @Offset;
            """;

        DynamicParameters parameters = ScopeColumns.AddParameters(
            new DynamicParameters(new { ScheduleId = scheduleId.Value, PageSize = paggination.Size.Value, Offset = paggination.Skip }), scopeFilter);

#pragma warning disable S2077
        SqlMapper.GridReader grid = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
#pragma warning restore S2077

        await using (grid)
        {
            int total = await grid.ReadSingleAsync<int>();
            IReadOnlyCollection<TaskRunListItem> runs = [.. (await grid.ReadAsync<RunRow>()).Select(ToItem)];

            return new ListQueryResponse<TaskRunListItem>(runs, total);
        }
    }

    // The run list's sort and filter fields, keyed by TaskRunListFields, over the reachable runs below (r): status is
    // the shown outcome, computed over r's unfinished count, so filter and column read the one expression.
    private static readonly ListQueryColumns RunListColumns = new(
        TaskRunListFields.Set,
        new Dictionary<string, string>
        {
            [TaskRunListFields.StartedAt] = "r.started_at_utc",
            [TaskRunListFields.Task] = "r.command_name",
            [TaskRunListFields.Trigger] = "r.\"trigger\"",
            [TaskRunListFields.Status] = OutcomeSql,
            [TaskRunListFields.StartedBy] = "r.acting_user_id",
            // Resolved to task names by the handler before the query is built; never reaches the criteria.
            [TaskRunListFields.Aggregate] = "r.command_name",
        },
        searchColumns: [],
        tieBreaker: "r.id");

    // RunOutcomes.Of, clause for clause, answering the RunOutcome member names: Skipped by status; Running while any item
    // is unfinished; with no failure Cancelled if any item was cancelled, else Succeeded; with failures Failed if none
    // succeeded, else PartlyFailed. r.unfinished is the reachable runs' count of unfinished items, the activity read's.
    private const string OutcomeSql =
        "(CASE WHEN r.status = 'Skipped' THEN 'Skipped' "
        + "WHEN r.unfinished > 0 THEN 'Running' "
        + "WHEN r.failed = 0 THEN (CASE WHEN r.cancelled > 0 THEN 'Cancelled' ELSE 'Succeeded' END) "
        + "WHEN r.succeeded = 0 THEN 'Failed' "
        + "ELSE 'PartlyFailed' END)";

    // Newest first, as every other run read, when the request names no sort.
    private const string DefaultRunListOrderBy = "ORDER BY r.started_at_utc DESC, r.id DESC";

    public async Task<ErrorOr<ListQueryResponse<TaskRunsListItem>>> RunsListAsync(string schema, ListQuery query, IReadOnlyCollection<string>? taskNames, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!TryQuote(schema, out string quoted))
        {
            return UnknownSchema;
        }

        using DbConnection connection = await connectionFactory.CreateConnectionAsync();

        DynamicParameters parameters = ScopeColumns.AddParameters(
            new DynamicParameters(new { PageSize = query.Paggination.Size.Value, Offset = query.Paggination.Skip }), scopeFilter);

        ListQuerySqlParts criteria = ListQuerySql.Apply(query, RunListColumns, parameters);
        string orderBy = query.Sorting?.Sorts.Any() == true ? criteria.OrderBy : DefaultRunListOrderBy;

        string where = criteria.Where;

        if (taskNames is not null)
        {
            where += " AND r.command_name = ANY(@AggregateTasks)";
            parameters.Add("AggregateTasks", taskNames.ToArray());
        }

        // The reachable runs, with RecentRunsAsync's reach predicate as it stands there: a schedule's run through its live
        // schedule, any other run through its own scope. The inner query reads task_runs alone, so in each branch Reach's
        // unqualified columns bind to the table that owns them; the joins below sit outside it, where the schedule's own
        // scope columns cannot capture them. The criteria read the inner query's columns, so count and page read the
        // same rows.
        string reachable =
            $$"""
            FROM (
                SELECT r.*,
                       -- Only a Running run can hold unfinished items, so no other run pays for the sub-count.
                       CASE WHEN r.status = 'Running' THEN
                           (SELECT count(*) FROM {{quoted}}.task_run_items u
                            WHERE u.run_id = r.id AND u.status IN ('Pending', 'Running', 'Retrying'))::int
                       ELSE 0 END AS unfinished
                FROM {{quoted}}.task_runs r
                WHERE ((r.schedule_id IS NOT NULL AND EXISTS (
                            SELECT 1 FROM {{quoted}}.task_schedules
                            WHERE id = r.schedule_id AND deleted_at_utc IS NULL AND {{ScopeColumns.ReachSql}}))
                       OR (r.schedule_id IS NULL AND {{ScopeColumns.ReachSql}}))) r
            """;

        // The lateral reads the one item of a one-item run, as the activity read does; the schedule join names a cron
        // run's live schedule, and is null for a one-off run.
        string sql =
            $$"""
            SELECT count(*)
            {{reachable}}
            WHERE TRUE{{where}};

            SELECT r.id AS Id, r.command_name AS CommandName, r."trigger" AS Trigger, r.acting_user_id AS ActingUserId,
                   r.schedule_id AS ScheduleId, s.cron AS ScheduleCron, s.time_zone_id AS ScheduleTimeZoneId,
                   {{OutcomeSql}} AS Outcome, r.started_at_utc AS StartedAtUtc, r.completed_at_utc AS CompletedAtUtc,
                   r.total AS Total, r.succeeded AS Succeeded, r.failed AS Failed, r.cancelled AS Cancelled,
                   r.unfinished AS Unfinished, one.target_id AS TargetId, one.target_label AS TargetLabel,
                   one.progress::int AS Progress,
                   CASE WHEN r.target_kind = 'Collection' THEN r.scope_id END AS ScopeId
            {{reachable}}
            LEFT JOIN LATERAL (
                SELECT i.target_id, i.target_label, i.progress
                FROM {{quoted}}.task_run_items i
                WHERE i.run_id = r.id AND r.total = 1
                ORDER BY i.id
                LIMIT 1) one ON true
            LEFT JOIN {{quoted}}.task_schedules s ON s.id = r.schedule_id AND s.deleted_at_utc IS NULL
            WHERE TRUE{{where}}
            {{orderBy}}
            LIMIT @PageSize OFFSET @Offset;
            """;

        // The schema was matched against the registered ones and the criteria hold only RunListColumns' expressions;
        // every value is a parameter.
#pragma warning disable S2077
        SqlMapper.GridReader grid = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
#pragma warning restore S2077

        await using (grid)
        {
            int total = await grid.ReadSingleAsync<int>();
            IReadOnlyCollection<TaskRunsListItem> runs = [.. (await grid.ReadAsync<RunsListRow>()).Select(ToListItem)];

            return new ListQueryResponse<TaskRunsListItem>(runs, total);
        }
    }

    private TaskRunsListItem ToListItem(RunsListRow row) =>
        new(
            row.Id,
            row.CommandName,
            Enum.Parse<TaskRunTrigger>(row.Trigger),
            row.ActingUserId,
            row.ScheduleId,
            row.ScheduleCron,
            row.ScheduleTimeZoneId,
            Enum.Parse<RunOutcome>(row.Outcome),
            row.StartedAtUtc,
            row.CompletedAtUtc,
            row.Total,
            row.Succeeded,
            row.Failed,
            row.Cancelled,
            row.Unfinished,
            row.TargetId,
            row.TargetLabel,
            row.Progress,
            CancelableOf(row.CommandName),
            row.ScopeId);

    public async Task<ErrorOr<IReadOnlyCollection<Guid>>> RunActorsAsync(string schema, CancellationToken ct)
    {
        if (!TryQuote(schema, out string quoted))
        {
            return UnknownSchema;
        }

        using DbConnection connection = await connectionFactory.CreateConnectionAsync();

        // The reach of the run list: a schedule's run through its live schedule, any other run through its own scope. The
        // inner query reads task_runs alone, so Reach's unqualified columns bind to the table that owns them in each branch.
        string sql =
            $$"""
            SELECT DISTINCT r.acting_user_id
            FROM (
                SELECT r.acting_user_id
                FROM {{quoted}}.task_runs r
                WHERE ((r.schedule_id IS NOT NULL AND EXISTS (
                            SELECT 1 FROM {{quoted}}.task_schedules
                            WHERE id = r.schedule_id AND deleted_at_utc IS NULL AND {{ScopeColumns.ReachSql}}))
                       OR (r.schedule_id IS NULL AND {{ScopeColumns.ReachSql}}))) r
            ORDER BY r.acting_user_id;
            """;

        DynamicParameters parameters = ScopeColumns.AddParameters(new DynamicParameters(), scopeFilter);

#pragma warning disable S2077
        IEnumerable<Guid> actors = await connection.QueryAsync<Guid>(new CommandDefinition(sql, parameters, cancellationToken: ct));
#pragma warning restore S2077

        IReadOnlyCollection<Guid> list = [.. actors];

        return list.ToErrorOr();
    }

    public async Task<ErrorOr<IReadOnlyCollection<TaskRunItemListItem>>> RunItemsAsync(string schema, Guid runId, CancellationToken ct)
    {
        if (!TryQuote(schema, out string quoted))
        {
            return UnknownSchema;
        }

        using DbConnection connection = await connectionFactory.CreateConnectionAsync();

        // A task's run is reached through its live task; a one-off run (no task) through its own scope. In each
        // branch the nearest table to Reach's unqualified columns is the one that owns them.
        string sql =
            $$"""
            SELECT i.target_id AS TargetId, i.status AS Status, i.attempts AS Attempts, i.error_code AS ErrorCode,
                   i.error_description AS ErrorDescription, i.completed_at_utc AS CompletedAtUtc,
                   i.progress::int AS Progress, i.target_label AS TargetLabel
            FROM {{quoted}}.task_run_items i
            WHERE i.run_id = @RunId
              AND EXISTS (
                  SELECT 1 FROM {{quoted}}.task_runs r
                  WHERE r.id = @RunId
                    AND ((r.schedule_id IS NOT NULL AND EXISTS (
                            SELECT 1 FROM {{quoted}}.task_schedules
                            WHERE id = r.schedule_id AND deleted_at_utc IS NULL AND {{ScopeColumns.ReachSql}}))
                         OR (r.schedule_id IS NULL AND {{ScopeColumns.ReachSql}})))
            ORDER BY i.id;
            """;

        DynamicParameters parameters = ScopeColumns.AddParameters(new DynamicParameters(new { RunId = runId }), scopeFilter);

#pragma warning disable S2077
        IEnumerable<ItemRow> rows = await connection.QueryAsync<ItemRow>(new CommandDefinition(sql, parameters, cancellationToken: ct));
#pragma warning restore S2077

        IReadOnlyCollection<TaskRunItemListItem> items =
        [
            .. rows.Select(row => new TaskRunItemListItem(
                row.TargetId,
                Enum.Parse<TaskRunItemStatus>(row.Status),
                row.Attempts,
                row.ErrorCode,
                row.ErrorDescription,
                row.CompletedAtUtc,
                row.Progress,
                row.TargetLabel)),
        ];

        return items.ToErrorOr();
    }

    public Task<ErrorOr<IReadOnlyCollection<TaskRunListItem>>> RecentRunsForTargetAsync(
        string schema, Guid targetId, int take, CancellationToken ct) =>
        RecentRunsAsync(
            schema,
            quoted => $"EXISTS (SELECT 1 FROM {quoted}.task_run_items i WHERE i.run_id = r.id AND i.target_id = @TargetId)",
            new DynamicParameters(new { TargetId = targetId, Take = Math.Clamp(take, 1, MaxRecentTake) }),
            ct);

    public Task<ErrorOr<IReadOnlyCollection<TaskRunListItem>>> RecentRunsForCollectionAsync(
        string schema, string taskName, Guid scopeId, int take, CancellationToken ct) =>
        RecentRunsAsync(
            schema,
            _ => "r.target_kind = 'Collection' AND r.command_name = @TaskName AND r.scope_id = @ScopeId",
            new DynamicParameters(new { TaskName = taskName, ScopeId = scopeId, Take = Math.Clamp(take, 1, MaxRecentTake) }),
            ct);

    private async Task<ErrorOr<IReadOnlyCollection<TaskRunListItem>>> RecentRunsAsync(
        string schema, Func<string, string> filterOf, DynamicParameters parameters, CancellationToken ct)
    {
        if (!TryQuote(schema, out string quoted))
        {
            return UnknownSchema;
        }

        using DbConnection connection = await connectionFactory.CreateConnectionAsync();

        // Every trigger: a schedule's run is reached through its live schedule (a deleted one hides its history, as in
        // the runs dialog), any other run through its own scope. In each branch the nearest table to Reach's unqualified
        // columns is the one that owns them.
        string sql =
            $$"""
            SELECT {{RunColumns}}
            FROM {{quoted}}.task_runs r
            WHERE ((r.schedule_id IS NOT NULL AND EXISTS (
                        SELECT 1 FROM {{quoted}}.task_schedules
                        WHERE id = r.schedule_id AND deleted_at_utc IS NULL AND {{ScopeColumns.ReachSql}}))
                   OR (r.schedule_id IS NULL AND {{ScopeColumns.ReachSql}}))
              AND {{filterOf(quoted)}}
            ORDER BY r.started_at_utc DESC, r.id DESC
            LIMIT @Take;
            """;

        ScopeColumns.AddParameters(parameters, scopeFilter);

#pragma warning disable S2077
        IEnumerable<RunRow> rows = await connection.QueryAsync<RunRow>(new CommandDefinition(sql, parameters, cancellationToken: ct));
#pragma warning restore S2077

        IReadOnlyCollection<TaskRunListItem> runs = [.. rows.Select(ToItem)];

        return runs.ToErrorOr();
    }

    public async Task<ErrorOr<IReadOnlyList<ActivityItem>>> ActivityForUserAsync(
        string schema, Guid userId, DateTime finishedSinceUtc, int take, CancellationToken ct)
    {
        if (!TryQuote(schema, out string quoted))
        {
            return UnknownSchema;
        }

        using DbConnection connection = await connectionFactory.CreateConnectionAsync();

        // No Reach: these are the caller's own runs, listed whatever their scope and the caller's grants now. The lateral
        // reads the one item of a one-item run; a run over several records names none of them. A Skipped run (an overlap
        // loser) ran nothing, so it is not activity.
        string sql =
            $$"""
            SELECT r.id AS RunId, r.command_name AS CommandName, r."trigger" AS Trigger, r.status AS Status,
                   r.started_at_utc AS StartedAtUtc, r.completed_at_utc AS CompletedAtUtc, r.total AS Total,
                   r.succeeded AS Succeeded, r.failed AS Failed, r.cancelled AS Cancelled,
                   (SELECT count(*) FROM {{quoted}}.task_run_items u
                    WHERE u.run_id = r.id AND u.status IN ('Pending', 'Running', 'Retrying'))::int AS Unfinished,
                   one.target_id AS TargetId, one.target_label AS TargetLabel,
                   one.error_code AS FirstErrorCode, one.error_description AS FirstErrorDescription,
                   one.progress::int AS Progress,
                   CASE WHEN r.target_kind = 'Collection' THEN r.scope_id END AS ScopeId
            FROM {{quoted}}.task_runs r
            LEFT JOIN LATERAL (
                SELECT i.target_id, i.target_label, i.error_code, i.error_description, i.progress
                FROM {{quoted}}.task_run_items i
                WHERE i.run_id = r.id AND r.total = 1
                ORDER BY i.id
                LIMIT 1) one ON true
            WHERE r.acting_user_id = @UserId AND r."trigger" IN ('Manual', 'ScheduleNow', 'Once') AND r.status <> 'Skipped'
              AND (r.status = 'Running' OR r.completed_at_utc >= @FinishedSince)
            ORDER BY r.started_at_utc DESC, r.id DESC
            LIMIT @Take;
            """;

        var parameters = new
        {
            UserId = userId,
            FinishedSince = AsUtc(finishedSinceUtc),
            Take = Math.Clamp(take, 1, MaxActivityTake),
        };

#pragma warning disable S2077
        IEnumerable<ActivityRow> rows = await connection.QueryAsync<ActivityRow>(new CommandDefinition(sql, parameters, cancellationToken: ct));
#pragma warning restore S2077

        IReadOnlyList<ActivityItem> items =
        [
            .. rows.Select(row => new ActivityItem(
                new TaskRunRef(schema, row.RunId),
                row.CommandName,
                Enum.Parse<TaskRunTrigger>(row.Trigger),
                Enum.Parse<TaskRunStatus>(row.Status),
                row.StartedAtUtc,
                row.CompletedAtUtc,
                row.Total,
                row.Succeeded,
                row.Failed,
                row.Unfinished,
                row.TargetId,
                row.TargetLabel,
                row.FirstErrorCode,
                row.FirstErrorDescription,
                row.Cancelled,
                row.Progress,
                CancelableOf(row.CommandName),
                row.ScopeId)),
        ];

        return items.ToErrorOr();
    }

    // The column is timestamptz: an unspecified kind is taken as UTC rather than sent as a local timestamp.
    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();

    private const string RunColumns =
        "r.id AS Id, r.command_name AS CommandName, r.\"trigger\" AS Trigger, r.acting_user_id AS ActingUserId, r.status AS Status, "
        + "r.started_at_utc AS StartedAtUtc, r.completed_at_utc AS CompletedAtUtc, r.total AS Total, "
        + "r.succeeded AS Succeeded, r.failed AS Failed, r.cancelled AS Cancelled, "
        + "CASE WHEN r.target_kind = 'Collection' THEN r.scope_id END AS ScopeId";

    // A name the catalogue no longer knows is cancelable: the cancel handler decides by the same rule.
    private bool CancelableOf(string commandName) => catalog.ByName(commandName)?.Settings.Cancelable ?? true;

    private TaskRunListItem ToItem(RunRow row) =>
        new(
            row.Id,
            row.CommandName,
            Enum.Parse<TaskRunTrigger>(row.Trigger),
            row.ActingUserId,
            Enum.Parse<TaskRunStatus>(row.Status),
            row.StartedAtUtc,
            row.CompletedAtUtc,
            row.Total,
            row.Succeeded,
            row.Failed,
            row.Cancelled,
            CancelableOf(row.CommandName),
            row.ScopeId);

    // Only a schema a module registered reaches the SQL text; the quoting is belt and braces over that match.
    private bool TryQuote(string schema, out string quoted)
    {
        bool known = schema is not null && registrations.Any(registration => string.Equals(registration.Schema, schema, StringComparison.Ordinal));
        quoted = known ? "\"" + schema!.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : string.Empty;

        return known;
    }

    // Dapper binds these by column alias and parses the enum text itself: it cannot bind a text column to an enum
    // constructor parameter.
    private sealed class TaskRow
    {
        public Guid Id { get; set; }

        public string CommandName { get; set; } = string.Empty;

        public string Cron { get; set; } = string.Empty;

        public string TimeZoneId { get; set; } = string.Empty;

        public string State { get; set; } = string.Empty;

        public DateTime? LastRunAtUtc { get; set; }

        public string? LastRunStatus { get; set; }

        public int? LastRunFailed { get; set; }

        public int TargetCount { get; set; }
    }

    private sealed class TaskDetailRow
    {
        public Guid Id { get; set; }

        public string CommandName { get; set; } = string.Empty;

        public string Arguments { get; set; } = string.Empty;

        public string Cron { get; set; } = string.Empty;

        public string TimeZoneId { get; set; } = string.Empty;

        public string State { get; set; } = string.Empty;

        public Guid DelegatingUserId { get; set; }

        public Guid RowVersion { get; set; }
    }

    private sealed class TargetRow
    {
        public Guid TargetId { get; set; }
    }

    private sealed class RunRow
    {
        public Guid Id { get; set; }

        public string CommandName { get; set; } = string.Empty;

        public string Trigger { get; set; } = string.Empty;

        public Guid ActingUserId { get; set; }

        public string Status { get; set; } = string.Empty;

        public DateTime StartedAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }

        public int Total { get; set; }

        public int Succeeded { get; set; }

        public int Failed { get; set; }

        public int Cancelled { get; set; }

        public Guid? ScopeId { get; set; }
    }

    private sealed class RunsListRow
    {
        public Guid Id { get; set; }

        public string CommandName { get; set; } = string.Empty;

        public string Trigger { get; set; } = string.Empty;

        public Guid ActingUserId { get; set; }

        public Guid? ScheduleId { get; set; }

        public string? ScheduleCron { get; set; }

        public string? ScheduleTimeZoneId { get; set; }

        public string Outcome { get; set; } = string.Empty;

        public DateTime StartedAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }

        public int Total { get; set; }

        public int Succeeded { get; set; }

        public int Failed { get; set; }

        public int Cancelled { get; set; }

        public int Unfinished { get; set; }

        public Guid? TargetId { get; set; }

        public string? TargetLabel { get; set; }

        public int? Progress { get; set; }

        public Guid? ScopeId { get; set; }
    }

    private sealed class ItemRow
    {
        public Guid? TargetId { get; set; }

        public string Status { get; set; } = string.Empty;

        public int Attempts { get; set; }

        public string? ErrorCode { get; set; }

        public string? ErrorDescription { get; set; }

        public DateTime? CompletedAtUtc { get; set; }

        public int? Progress { get; set; }

        public string? TargetLabel { get; set; }
    }

    private sealed class ActivityRow
    {
        public Guid RunId { get; set; }

        public string CommandName { get; set; } = string.Empty;

        public string Trigger { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime StartedAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }

        public int Total { get; set; }

        public int Succeeded { get; set; }

        public int Failed { get; set; }

        public int Unfinished { get; set; }

        public Guid? TargetId { get; set; }

        public string? TargetLabel { get; set; }

        public string? FirstErrorCode { get; set; }

        public string? FirstErrorDescription { get; set; }

        public int Cancelled { get; set; }

        public int? Progress { get; set; }

        public Guid? ScopeId { get; set; }
    }
}
