using Sergin.SharedKernel.Domain.Users;

namespace Sergin.SharedKernel.Domain.Tasks;

/// <summary>
/// One send of a command to its targets: a scheduled firing, a "Run now", or a one-off run with no stored task. The
/// command and its arguments are copied onto the run, so history stays true after the task is edited or deleted.
/// Not an aggregate: no audit, soft delete or version; purged after the retention period.
/// </summary>
public sealed class TaskRun
{
    private TaskRun()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>The task this run belongs to; null for a one-off run.</summary>
    public TaskScheduleId? ScheduleId { get; private set; }

    public TaskRunTrigger Trigger { get; private set; }

    /// <summary>The person the run acts for: the task's delegating user, or whoever clicked "Run now".</summary>
    public UserId ActingUserId { get; private set; } = default!;

    public Guid ScopeId { get; private set; }

    public string ScopePath { get; private set; } = string.Empty;

    public string CommandName { get; private set; } = string.Empty;

    public string Arguments { get; private set; } = string.Empty;

    /// <summary>
    /// Whether the run's items each target a record (<see cref="TaskRunTargetKind.Entity"/>) or the run has one item with
    /// no target (<see cref="TaskRunTargetKind.Collection"/>, an <c>ITaskCommand&lt;TRoot&gt;</c>). A collection task has at
    /// most one Running run per task name and scope.
    /// </summary>
    public TaskRunTargetKind TargetKind { get; private set; }

    public TaskRunStatus Status { get; private set; }

    public DateTime StartedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public int Total { get; private set; }

    public int Succeeded { get; private set; }

    public int Failed { get; private set; }

    /// <summary>The items cancelled before they ran; counted with the others when the run completes.</summary>
    public int Cancelled { get; private set; }

    /// <summary>When someone asked to cancel the run; null while nobody has. Stamped once, by the store, under the run's lock.</summary>
    public DateTime? CancelRequestedAtUtc { get; private set; }

    public static TaskRun Start(TaskRunDefinition definition, int total, DateTime startedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentOutOfRangeException.ThrowIfNegative(total);

        TaskRun run = From(definition, startedAtUtc);
        run.Status = TaskRunStatus.Running;
        run.Total = total;

        return run;
    }

    /// <summary>A run that did not send anything (the task was paused, deleted or overlapping); complete at once.</summary>
    public static TaskRun Skipped(TaskRunDefinition definition, DateTime at)
    {
        ArgumentNullException.ThrowIfNull(definition);

        TaskRun run = From(definition, at);
        run.Status = TaskRunStatus.Skipped;
        run.CompletedAtUtc = at;

        return run;
    }

    public void Complete(int succeeded, int failed, int cancelled, DateTime at)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(succeeded);
        ArgumentOutOfRangeException.ThrowIfNegative(failed);
        ArgumentOutOfRangeException.ThrowIfNegative(cancelled);

        Status = TaskRunStatus.Completed;
        Succeeded = succeeded;
        Failed = failed;
        Cancelled = cancelled;
        CompletedAtUtc = at;
    }

    private static TaskRun From(TaskRunDefinition definition, DateTime startedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(definition.ActingUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.ScopePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.CommandName);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Arguments);

        return new TaskRun
        {
            Id = Guid.CreateVersion7(),
            ScheduleId = definition.ScheduleId,
            Trigger = definition.Trigger,
            ActingUserId = definition.ActingUserId,
            ScopeId = definition.ScopeId,
            ScopePath = definition.ScopePath,
            CommandName = definition.CommandName,
            Arguments = definition.Arguments,
            TargetKind = definition.TargetKind,
            StartedAtUtc = startedAtUtc,
        };
    }
}

/// <summary>What a run sends, for whom and where: the part of a run fixed when it starts.</summary>
public sealed record TaskRunDefinition(
    TaskScheduleId? ScheduleId,
    TaskRunTrigger Trigger,
    UserId ActingUserId,
    Guid ScopeId,
    string ScopePath,
    string CommandName,
    string Arguments,
    TaskRunTargetKind TargetKind = TaskRunTargetKind.Entity);

/// <summary>What a run's items target; stored as its name.</summary>
public enum TaskRunTargetKind
{
    /// <summary>One item per picked record, each with its target id.</summary>
    Entity,

    /// <summary>One item with no target: a task over the whole collection in the run's scope.</summary>
    Collection,
}

public enum TaskRunTrigger
{
    /// <summary>A schedule's cron fired.</summary>
    Schedule,

    /// <summary>A schedule run at once ("Run now").</summary>
    ScheduleNow,

    /// <summary>A one-off run with no stored schedule.</summary>
    Once,

    /// <summary>A task started by a person: no schedule, one item per picked record.</summary>
    Manual,
}

public enum TaskRunStatus
{
    Running,
    Completed,
    Skipped,
}
