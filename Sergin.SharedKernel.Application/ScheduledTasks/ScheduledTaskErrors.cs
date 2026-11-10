namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>The errors a scheduled task or one of its run items can end with.</summary>
public static class ScheduledTaskErrors
{
    /// <summary>The run item's code once every attempt threw.</summary>
    public const string ExhaustedCode = "ScheduledTasks.Exhausted";

    /// <summary>A stored command name no configuration declares any more.</summary>
    public static Error UnknownCommand { get; } =
        Error.Failure("ScheduledTasks.UnknownCommand", "The scheduled command is not declared by any module.");

    /// <summary>Stored arguments that no longer fit the command's shape.</summary>
    public static Error ArgumentsInvalid { get; } =
        Error.Failure("ScheduledTasks.ArgumentsInvalid", "The scheduled command's stored arguments no longer fit its shape.");

    /// <summary>A run was saved but the engine could not take its items; they are failed so the run can finish.</summary>
    public static Error EngineUnavailable { get; } =
        Error.Unexpected("ScheduledTasks.EngineUnavailable", "The job engine could not accept the run's items.");

    /// <summary>
    /// The engine refused a schedule's trigger while it was created or changed; nothing was saved. The same code as
    /// <see cref="EngineUnavailable"/>: to a person it is the same failure.
    /// </summary>
    public static Error TriggerEngineUnavailable { get; } =
        Error.Unexpected("ScheduledTasks.EngineUnavailable", "The job engine could not accept the schedule; nothing was saved.");

    /// <summary>Another run of the same task started first; the overlap rule skips this one.</summary>
    public static Error Overlap { get; } =
        Error.Conflict("ScheduledTasks.Overlap", "A previous run of this task is still unfinished.");

    /// <summary>A command that did not opt in with <c>Schedulable(name)</c>.</summary>
    public static Error NotSchedulable { get; } =
        Error.Validation("ScheduledTasks.NotSchedulable", "This command cannot be scheduled.");

    /// <summary>More targets than a command that runs once per selected row may take, or more than one for one that does not.</summary>
    public static Error TooManyTargets { get; } =
        Error.Validation("ScheduledTasks.TooManyTargets", "This command cannot be scheduled for that many records.");
}
