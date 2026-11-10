using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Queries;

/// <summary>One row of a record's schedule list; the last-run columns are null until the task has run.</summary>
public sealed record ScheduledTaskListItem(
    Guid Id,
    string CommandName,
    string Cron,
    string TimeZoneId,
    ScheduledTaskState State,
    DateTime? LastRunAtUtc,
    ScheduledRunStatus? LastRunStatus,
    int? LastRunFailed,
    int TargetCount);

public sealed record ScheduledTaskResponse(
    Guid Id,
    string CommandName,
    string Arguments,
    string Cron,
    string TimeZoneId,
    ScheduledTaskState State,
    Guid DelegatingUserId,
    IReadOnlyCollection<ScheduledTaskTarget> Targets);

public sealed record ScheduledRunListItem(
    Guid Id,
    ScheduledRunTrigger Trigger,
    Guid ActingUserId,
    ScheduledRunStatus Status,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    int Total,
    int Succeeded,
    int Failed);

public sealed record ScheduledRunItemListItem(
    Guid TargetId,
    ScheduledRunItemStatus Status,
    int Attempts,
    string? ErrorCode,
    string? ErrorDescription,
    DateTime? CompletedAtUtc);
