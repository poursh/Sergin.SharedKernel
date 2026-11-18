using Sergin.SharedKernel.Application.ScheduledTasks.Commands;
using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Activity;

/// <summary>
/// One run a person started (Background, Run now or Run once), as their activity shows it. <see cref="Unfinished"/>
/// counts the items still Pending, Running or Retrying. The target fields and <see cref="FirstErrorCode"/> are set only
/// for a one-item run: a run over several records names none of them.
/// </summary>
public sealed record ActivityItem(
    BackgroundRunRef Ref,
    string CommandName,
    ScheduledRunTrigger Trigger,
    ScheduledRunStatus Status,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    int Total,
    int Succeeded,
    int Failed,
    int Unfinished,
    Guid? TargetId,
    Guid? RootId,
    string? TargetLabel,
    string? FirstErrorCode);
