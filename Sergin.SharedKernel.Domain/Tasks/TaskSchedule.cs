using Sergin.SharedKernel.Domain.Users;

namespace Sergin.SharedKernel.Domain.Tasks;

/// <summary>
/// A saved schedule: one schedulable task, by its stable name, with its non-target arguments as JSON, sent to a
/// fixed set of picked records on a cron in a time zone. Scheduled runs act on behalf of <see cref="DelegatingUserId"/>,
/// the user who last saved it. Owned by the module whose command it sends, and stored in that module's schema; see
/// <c>ITasksDbContext</c>.
/// </summary>
public sealed class TaskSchedule : AggregateRoot<TaskScheduleId>
{
    private readonly List<TaskScheduleTarget> targets = [];

    private TaskSchedule()
    {
    }

    /// <summary>The task's stable name (<c>TaskSettings.Name</c>), never a CLR type name.</summary>
    public string CommandName { get; private set; } = string.Empty;

    /// <summary>The command's non-target arguments, as JSON.</summary>
    public string Arguments { get; private set; } = string.Empty;

    public string Cron { get; private set; } = string.Empty;

    /// <summary>The IANA (or Windows) id the cron is read in.</summary>
    public string TimeZoneId { get; private set; } = string.Empty;

    public TaskScheduleState State { get; private set; }

    /// <summary>Whoever last saved the task; every scheduled run acts on behalf of this user.</summary>
    public UserId DelegatingUserId { get; private set; } = default!;

    /// <summary>The engine's trigger for this task, the only link to the engine; null until linked.</summary>
    public Guid? EngineTriggerId { get; private set; }

    public IReadOnlyCollection<TaskScheduleTarget> Targets => targets;

    public static TaskSchedule Create(
        string commandName, string arguments, IReadOnlyCollection<TaskScheduleTarget> targets, string cron, string timeZoneId, UserId savedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);

        var task = new TaskSchedule
        {
            Id = new TaskScheduleId(Guid.CreateVersion7()),
            CommandName = commandName,
            State = TaskScheduleState.Active,
        };

        task.Update(arguments, targets, cron, timeZoneId, savedBy);

        return task;
    }

    /// <summary>Replaces the arguments, targets and trigger; the saver becomes the delegating user.</summary>
    public void Update(string arguments, IReadOnlyCollection<TaskScheduleTarget> targets, string cron, string timeZoneId, UserId savedBy)
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

    public void Pause() => State = TaskScheduleState.Paused;

    public void Resume() => State = TaskScheduleState.Active;

    public void LinkTrigger(Guid engineTriggerId) => EngineTriggerId = engineTriggerId;
}

public sealed record TaskScheduleId(Guid Value);

/// <summary>One picked record: the target's id.</summary>
public sealed record TaskScheduleTarget(Guid TargetId);

public enum TaskScheduleState
{
    Active,
    Paused,
}
