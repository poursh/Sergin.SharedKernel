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

    /// <summary>A command that did not opt in with <c>Schedulable(name)</c>.</summary>
    public static Error NotSchedulable { get; } =
        Error.Validation("ScheduledTasks.NotSchedulable", "This command cannot be scheduled.");

    /// <summary>More targets than a command that runs once per selected row may take, or more than one for one that does not.</summary>
    public static Error TooManyTargets { get; } =
        Error.Validation("ScheduledTasks.TooManyTargets", "This command cannot be scheduled for that many records.");
}
