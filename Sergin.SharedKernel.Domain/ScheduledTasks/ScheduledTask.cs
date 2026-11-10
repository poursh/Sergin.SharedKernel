using Sergin.SharedKernel.Domain.Users;

namespace Sergin.SharedKernel.Domain.ScheduledTasks;

/// <summary>
/// A saved schedule: one schedulable command, by its stable name, with its non-target arguments as JSON, sent to a
/// fixed set of picked records on a cron in a time zone. Scheduled runs act on behalf of <see cref="DelegatingUserId"/>,
/// the user who last saved it. Owned by the module whose command it sends, and stored in that module's schema; see
/// <c>IScheduledTasksDbContext</c>.
/// </summary>
public sealed class ScheduledTask : AggregateRoot<ScheduledTaskId>
{
    private readonly List<ScheduledTaskTarget> targets = [];

    private ScheduledTask()
    {
    }

    /// <summary>The command's stable declared name (<c>Schedulable(name)</c>), never a CLR type name.</summary>
    public string CommandName { get; private set; } = string.Empty;

    /// <summary>The command's non-target arguments, as JSON.</summary>
    public string Arguments { get; private set; } = string.Empty;

    public string Cron { get; private set; } = string.Empty;

    /// <summary>The IANA (or Windows) id the cron is read in.</summary>
    public string TimeZoneId { get; private set; } = string.Empty;

    public ScheduledTaskState State { get; private set; }

    /// <summary>Whoever last saved the task; every scheduled run acts on behalf of this user.</summary>
    public UserId DelegatingUserId { get; private set; } = default!;

    /// <summary>The engine's trigger for this task, the only link to the engine; null until linked.</summary>
    public Guid? EngineTriggerId { get; private set; }

    public IReadOnlyCollection<ScheduledTaskTarget> Targets => targets;

    public static ScheduledTask Create(
        string commandName, string arguments, IReadOnlyCollection<ScheduledTaskTarget> targets, string cron, string timeZoneId, UserId savedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);

        var task = new ScheduledTask
        {
            Id = new ScheduledTaskId(Guid.CreateVersion7()),
            CommandName = commandName,
            State = ScheduledTaskState.Active,
        };

        task.Update(arguments, targets, cron, timeZoneId, savedBy);

        return task;
    }

    /// <summary>Replaces the arguments, targets and trigger; the saver becomes the delegating user.</summary>
    public void Update(string arguments, IReadOnlyCollection<ScheduledTaskTarget> targets, string cron, string timeZoneId, UserId savedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arguments);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentException.ThrowIfNullOrWhiteSpace(cron);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        ArgumentNullException.ThrowIfNull(savedBy);

        // Kept rows stay tracked as they are: re-adding a target with the same key would collide in the change tracker.
        this.targets.RemoveAll(target => !targets.Contains(target));
        this.targets.AddRange(targets.Distinct().Where(target => !this.targets.Contains(target)));

        Arguments = arguments;
        Cron = cron;
        TimeZoneId = timeZoneId;
        DelegatingUserId = savedBy;
    }

    public void Pause() => State = ScheduledTaskState.Paused;

    public void Resume() => State = ScheduledTaskState.Active;

    public void LinkTrigger(Guid engineTriggerId) => EngineTriggerId = engineTriggerId;
}

public sealed record ScheduledTaskId(Guid Value);

/// <summary>One picked record: the target's id, and its root's for a child-entity command.</summary>
public sealed record ScheduledTaskTarget(Guid TargetId, Guid? RootId);

public enum ScheduledTaskState
{
    Active,
    Paused,
}
