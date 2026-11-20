using Sergin.SharedKernel.Domain.Localizations;

namespace Sergin.SharedKernel.Application.Tasks;

/// <summary>The errors a scheduled task or one of its run items can end with.</summary>
public static class TaskErrors
{
    /// <summary>The run item's code once every attempt threw.</summary>
    public const string ExhaustedCode = "Tasks.Exhausted";

    /// <summary>The run item's code when a background command ran longer than its declared timeout; final, never retried.</summary>
    public const string TimedOutCode = "Tasks.TimedOut";

    /// <summary>A background command cancelled at its declared timeout.</summary>
    public static Error TimedOut(TimeSpan timeout) =>
        Error.Failure(
            TimedOutCode,
            $"The command ran longer than its timeout of {timeout.ToString("c", System.Globalization.CultureInfo.InvariantCulture)}.");

    /// <summary>A stored command name no configuration declares any more.</summary>
    public static Error UnknownCommand { get; } =
        Error.Failure("Tasks.UnknownCommand", "The scheduled command is not declared by any module.");

    /// <summary>Stored arguments that no longer fit the command's shape.</summary>
    public static Error ArgumentsInvalid { get; } =
        Error.Failure("Tasks.ArgumentsInvalid", "The scheduled command's stored arguments no longer fit its shape.");

    /// <summary>A run was saved but the engine could not take its items; they are failed so the run can finish.</summary>
    public static Error EngineUnavailable { get; } =
        Error.Unexpected("Tasks.EngineUnavailable", "The job engine could not accept the run's items.");

    /// <summary>
    /// The engine refused a schedule's trigger while it was created or changed; nothing was saved. The same code as
    /// <see cref="EngineUnavailable"/>: to a person it is the same failure.
    /// </summary>
    public static Error TriggerEngineUnavailable { get; } =
        Error.Unexpected("Tasks.EngineUnavailable", "The job engine could not accept the schedule; nothing was saved.");

    /// <summary>Another run of the same task started first; the overlap rule skips this one.</summary>
    public static Error Overlap { get; } =
        Error.Conflict("Tasks.Overlap", "A previous run of this task is still unfinished.");

    /// <summary>A command that did not opt in with <c>Schedulable(name)</c>.</summary>
    public static Error NotSchedulable { get; } =
        Error.Validation("Tasks.NotSchedulable", "This command cannot be scheduled.");

    /// <summary>A command started in the background whose configuration does not call <c>Background()</c>.</summary>
    public static Error NotBackground { get; } =
        Error.Validation("Tasks.NotBackground", TaskTexts.NotBackground.Default);

    /// <summary>More targets than a command that runs once per selected row may take, or more than one for one that does not.</summary>
    public static Error TooManyTargets { get; } =
        Error.Validation("Tasks.TooManyTargets", "This command cannot be scheduled for that many records.");

    /// <summary>A cancel of a run that is no longer running: it completed, or was skipped.</summary>
    public static Error RunFinished { get; } =
        Error.Conflict(
            "Tasks.RunFinished",
            TaskTexts.RunFinished.Default,
            ErrorText.Of(TaskTexts.RunFinished.Key.Value));

    /// <summary>A background command sent inline instead of started as a run.</summary>
    public static Error MustRunInBackground { get; } =
        Error.Validation(
            "Tasks.MustRunInBackground",
            TaskTexts.MustRunInBackground.Default,
            ErrorText.Of(TaskTexts.MustRunInBackground.Key.Value));
}
