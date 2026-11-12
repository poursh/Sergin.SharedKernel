using Sergin.SharedKernel.Domain.Users;

namespace Sergin.SharedKernel.Domain.ScheduledTasks;

/// <summary>
/// One send of a command to its targets: a scheduled firing, a "Run now", or a one-off run with no stored task. The
/// command and its arguments are copied onto the run, so history stays true after the task is edited or deleted.
/// Not an aggregate: no audit, soft delete or version; purged after the retention period.
/// </summary>
public sealed class ScheduledTaskRun
{
    private ScheduledTaskRun()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>The task this run belongs to; null for a one-off run.</summary>
    public ScheduledTaskId? TaskId { get; private set; }

    public ScheduledRunTrigger Trigger { get; private set; }

    /// <summary>The person the run acts for: the task's delegating user, or whoever clicked "Run now".</summary>
    public UserId ActingUserId { get; private set; } = default!;

    public Guid ScopeId { get; private set; }

    public string ScopePath { get; private set; } = string.Empty;

    public string CommandName { get; private set; } = string.Empty;

    public string Arguments { get; private set; } = string.Empty;

    public ScheduledRunStatus Status { get; private set; }

    public DateTime StartedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public int Total { get; private set; }

    public int Succeeded { get; private set; }

    public int Failed { get; private set; }

    public static ScheduledTaskRun Start(ScheduledRunDefinition definition, int total, DateTime startedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentOutOfRangeException.ThrowIfNegative(total);

        ScheduledTaskRun run = From(definition, startedAtUtc);
        run.Status = ScheduledRunStatus.Running;
        run.Total = total;

        return run;
    }

    /// <summary>A run that did not send anything (the task was paused, deleted or overlapping); complete at once.</summary>
    public static ScheduledTaskRun Skipped(ScheduledRunDefinition definition, DateTime at)
    {
        ArgumentNullException.ThrowIfNull(definition);

        ScheduledTaskRun run = From(definition, at);
        run.Status = ScheduledRunStatus.Skipped;
        run.CompletedAtUtc = at;

        return run;
    }

    public void Complete(int succeeded, int failed, DateTime at)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(succeeded);
        ArgumentOutOfRangeException.ThrowIfNegative(failed);

        Status = ScheduledRunStatus.Completed;
        Succeeded = succeeded;
        Failed = failed;
        CompletedAtUtc = at;
    }

    private static ScheduledTaskRun From(ScheduledRunDefinition definition, DateTime startedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(definition.ActingUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.ScopePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.CommandName);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Arguments);

        return new ScheduledTaskRun
        {
            Id = Guid.CreateVersion7(),
            TaskId = definition.TaskId,
            Trigger = definition.Trigger,
            ActingUserId = definition.ActingUserId,
            ScopeId = definition.ScopeId,
            ScopePath = definition.ScopePath,
            CommandName = definition.CommandName,
            Arguments = definition.Arguments,
            StartedAtUtc = startedAtUtc,
        };
    }
}

/// <summary>What a run sends, for whom and where: the part of a run fixed when it starts.</summary>
public sealed record ScheduledRunDefinition(
    ScheduledTaskId? TaskId,
    ScheduledRunTrigger Trigger,
    UserId ActingUserId,
    Guid ScopeId,
    string ScopePath,
    string CommandName,
    string Arguments);

public enum ScheduledRunTrigger
{
    Scheduled,
    RunNow,
    OneOff,
}

public enum ScheduledRunStatus
{
    Running,
    Completed,
    Skipped,
}
