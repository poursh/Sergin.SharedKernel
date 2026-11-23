using Sergin.SharedKernel.Domain.Tasks;

namespace Sergin.SharedKernel.Application.Tasks.Queries;

/// <summary>One row of a record's schedule list; the last-run columns are null until the task has run.</summary>
public sealed record TaskScheduleListItem(
    Guid Id,
    string CommandName,
    string Cron,
    string TimeZoneId,
    TaskScheduleState State,
    DateTime? LastRunAtUtc,
    TaskRunStatus? LastRunStatus,
    int? LastRunFailed,
    int TargetCount);

public sealed record TaskScheduleResponse(
    Guid Id,
    string CommandName,
    string Arguments,
    string Cron,
    string TimeZoneId,
    TaskScheduleState State,
    Guid DelegatingUserId,
    IReadOnlyCollection<TaskScheduleTarget> Targets);

/// <summary>
/// One run; <see cref="CommandName"/> is the task's stable name, as stored. <see cref="ScopeId"/> is the
/// run's scope for a collection task's run, whose one item has no target to name it by; null otherwise.
/// </summary>
public sealed record TaskRunListItem(
    Guid Id,
    string CommandName,
    TaskRunTrigger Trigger,
    Guid ActingUserId,
    TaskRunStatus Status,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    int Total,
    int Succeeded,
    int Failed,
    int Cancelled,
    bool Cancelable = true,
    Guid? ScopeId = null);

/// <summary>
/// One row of a module's run list. <see cref="Outcome"/> is the shown status, by the one rule (<see cref="RunOutcomes"/>);
/// <see cref="ScheduleCron"/> and <see cref="ScheduleTimeZoneId"/> are the live schedule's, null for a one-off run. The
/// target columns and <see cref="Progress"/> name a one-item run's record, null for a run over several;
/// <see cref="ScopeId"/> is a collection task's run's scope, null otherwise.
/// </summary>
public sealed record TaskRunsListItem(
    Guid Id,
    string CommandName,
    TaskRunTrigger Trigger,
    Guid ActingUserId,
    Guid? ScheduleId,
    string? ScheduleCron,
    string? ScheduleTimeZoneId,
    RunOutcome Outcome,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    int Total,
    int Succeeded,
    int Failed,
    int Cancelled,
    int Unfinished,
    Guid? TargetId,
    string? TargetLabel,
    int? Progress,
    bool Cancelable,
    Guid? ScopeId);

/// <summary>
/// One item of a run; <see cref="Progress"/> is its command's last reported percent, null until it reports, and
/// <see cref="TargetLabel"/> the text stored with the item when it was started, null when none was.
/// </summary>
public sealed record TaskRunItemListItem(
    Guid? TargetId,
    TaskRunItemStatus Status,
    int Attempts,
    string? ErrorCode,
    string? ErrorDescription,
    DateTime? CompletedAtUtc,
    int? Progress,
    string? TargetLabel = null);
