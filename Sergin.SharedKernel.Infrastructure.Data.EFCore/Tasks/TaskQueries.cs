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

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Tasks;

/// <summary>
/// The scheduled-task reads, raw SQL through <see cref="IDbConnectionFactory"/>, one connection per call, in the schema
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
    private const int MaxOneOffTake = 100;

    private const int MaxActivityTake = 100;

    private static readonly Error UnknownSchema = Error.Validation(
        "Tasks.UnknownSchema", "No module opted into scheduled tasks under that schema.");

    public async Task<ErrorOr<IReadOnlyCollection<TaskScheduleListItem>>> ForTargetAsync(string schema, Guid targetId, CancellationToken ct)
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
              AND EXISTS (SELECT 1 FROM {{quoted}}.task_schedule_targets y WHERE y.schedule_id = t.id AND y.target_id = @TargetId)
            ORDER BY t.id;
            """;

        DynamicParameters parameters = ScopeColumns.AddParameters(new DynamicParameters(new { TargetId = targetId }), scopeFilter);

        // The schema was matched against the registered ones; every value is a parameter.
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

            SELECT x.target_id AS TargetId, x.root_id AS RootId
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
                [.. (await grid.ReadAsync<TargetRow>()).Select(target => new TaskScheduleTarget(target.TargetId, target.RootId))];

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
                   i.progress::int AS Progress
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
                row.Progress)),
        ];

        return items.ToErrorOr();
    }

    public async Task<ErrorOr<IReadOnlyCollection<TaskRunListItem>>> OneOffRunsForTargetAsync(
        string schema, Guid targetId, int take, CancellationToken ct)
    {
        if (!TryQuote(schema, out string quoted))
        {
            return UnknownSchema;
        }

        using DbConnection connection = await connectionFactory.CreateConnectionAsync();

        // The outer query's only table is the runs, which carry their own scope.
        string sql =
            $$"""
            SELECT {{RunColumns}}
            FROM {{quoted}}.task_runs r
            WHERE r.schedule_id IS NULL AND r."trigger" IN ('Once', 'Manual') AND {{ScopeColumns.ReachSql}}
              AND EXISTS (SELECT 1 FROM {{quoted}}.task_run_items i WHERE i.run_id = r.id AND i.target_id = @TargetId)
            ORDER BY r.started_at_utc DESC, r.id DESC
            LIMIT @Take;
            """;

        DynamicParameters parameters = ScopeColumns.AddParameters(
            new DynamicParameters(new { TargetId = targetId, Take = Math.Clamp(take, 1, MaxOneOffTake) }), scopeFilter);

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
                   one.target_id AS TargetId, one.root_id AS RootId, one.target_label AS TargetLabel,
                   one.error_code AS FirstErrorCode, one.error_description AS FirstErrorDescription,
                   one.progress::int AS Progress
            FROM {{quoted}}.task_runs r
            LEFT JOIN LATERAL (
                SELECT i.target_id, i.root_id, i.target_label, i.error_code, i.error_description, i.progress
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
                row.RootId,
                row.TargetLabel,
                row.FirstErrorCode,
                row.FirstErrorDescription,
                row.Cancelled,
                row.Progress,
                CancelableOf(row.CommandName))),
        ];

        return items.ToErrorOr();
    }

    // The column is timestamptz: an unspecified kind is taken as UTC rather than sent as a local timestamp.
    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();

    private const string RunColumns =
        "r.id AS Id, r.command_name AS CommandName, r.\"trigger\" AS Trigger, r.acting_user_id AS ActingUserId, r.status AS Status, "
        + "r.started_at_utc AS StartedAtUtc, r.completed_at_utc AS CompletedAtUtc, r.total AS Total, "
        + "r.succeeded AS Succeeded, r.failed AS Failed, r.cancelled AS Cancelled";

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
            CancelableOf(row.CommandName));

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

        public Guid? RootId { get; set; }
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
    }

    private sealed class ItemRow
    {
        public Guid TargetId { get; set; }

        public string Status { get; set; } = string.Empty;

        public int Attempts { get; set; }

        public string? ErrorCode { get; set; }

        public string? ErrorDescription { get; set; }

        public DateTime? CompletedAtUtc { get; set; }

        public int? Progress { get; set; }
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

        public Guid? RootId { get; set; }

        public string? TargetLabel { get; set; }

        public string? FirstErrorCode { get; set; }

        public string? FirstErrorDescription { get; set; }

        public int Cancelled { get; set; }

        public int? Progress { get; set; }
    }
}
