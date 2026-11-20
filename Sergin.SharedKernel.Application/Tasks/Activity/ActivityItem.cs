using Sergin.SharedKernel.Application.Tasks.Commands;
using Sergin.SharedKernel.Domain.Tasks;

namespace Sergin.SharedKernel.Application.Tasks.Activity;

/// <summary>
/// One run a person started (Background, Run now or Run once), as their activity shows it. <see cref="Unfinished"/>
/// counts the items still Pending, Running or Retrying. The target fields, <see cref="FirstErrorCode"/> and
/// <see cref="FirstErrorDescription"/> (the item's stored error, as the runs dialog shows it) are set only for a one-item
/// run: a run over several records names none of them. <see cref="Cancelled"/> is the run's count of cancelled items
/// (zero until it completes). <see cref="Progress"/> is the one item's progress, 0–100, likewise only for a one-item run,
/// and null until its command reports.
/// </summary>
public sealed record ActivityItem(
    TaskRunRef Ref,
    string CommandName,
    TaskRunTrigger Trigger,
    TaskRunStatus Status,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    int Total,
    int Succeeded,
    int Failed,
    int Unfinished,
    Guid? TargetId,
    Guid? RootId,
    string? TargetLabel,
    string? FirstErrorCode,
    string? FirstErrorDescription,
    int Cancelled,
    int? Progress);
