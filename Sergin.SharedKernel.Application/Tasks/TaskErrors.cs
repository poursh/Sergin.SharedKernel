using System.Globalization;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Domain.Localizations;

namespace Sergin.SharedKernel.Application.Tasks;

/// <summary>The errors a scheduled task or one of its run items can end with.</summary>
public static class TaskErrors
{
    /// <summary>The run item's code once every attempt threw.</summary>
    public const string ExhaustedCode = "Tasks.Exhausted";

    /// <summary>The run item's code when its task reported no progress for its inactivity timeout; final, never retried.</summary>
    public const string TimedOutCode = "Tasks.TimedOut";

    /// <summary>The run item's code when its task ran past its <c>MaxDuration</c>; final, never retried.</summary>
    public const string MaxDurationExceededCode = "Tasks.MaxDurationExceeded";

    /// <summary>A task stopped after <paramref name="inactivity"/> without progress.</summary>
    public static Error TimedOut(TimeSpan inactivity) => Stopped(TimedOutCode, TaskTexts.TimedOut, "Timeout", inactivity);

    /// <summary>A task stopped at its <c>MaxDuration</c>, <paramref name="cap"/>, however it reported.</summary>
    public static Error MaxDurationExceeded(TimeSpan cap) =>
        Stopped(MaxDurationExceededCode, TaskTexts.MaxDurationExceeded, "MaxDuration", cap);

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

    /// <summary><see cref="NotSchedulable"/>'s code.</summary>
    public const string NotSchedulableCode = "Tasks.NotSchedulable";

    /// <summary><see cref="MustRunAsTask"/>'s code.</summary>
    public const string MustRunAsTaskCode = "Tasks.MustRunAsTask";

    /// <summary>
    /// A task that is not <c>Schedulable()</c> saved on a schedule or run once, or a saved schedule fired after its task
    /// lost the call: the run fails, the schedule stays.
    /// </summary>
    public static Error NotSchedulable { get; } =
        Error.Validation(NotSchedulableCode, TaskTexts.NotSchedulable.Default, ErrorText.Of(TaskTexts.NotSchedulable.Key.Value));

    /// <summary>A start target with no record id for a task that acts on one record; reported under <c>Targets</c>.</summary>
    public static Error TargetWithoutId { get; } =
        Error.Validation("Targets", TaskTexts.TargetWithoutId.Default, ErrorText.Of(TaskTexts.TargetWithoutId.Key.Value));

    /// <summary>More targets than a command that runs once per selected row may take, or more than one for one that does not.</summary>
    public static Error TooManyTargets { get; } =
        Error.Validation("Tasks.TooManyTargets", "This command cannot be scheduled for that many records.");

    /// <summary>A cancel of a run that is no longer running: it completed, or was skipped.</summary>
    public static Error RunFinished { get; } =
        Error.Conflict(
            "Tasks.RunFinished",
            TaskTexts.RunFinished.Default,
            ErrorText.Of(TaskTexts.RunFinished.Key.Value));

    /// <summary>A task (<c>ITaskCommand</c>) sent inline instead of started as a run; refused by its type.</summary>
    public static Error MustRunAsTask { get; } =
        Error.Validation(
            MustRunAsTaskCode,
            TaskTexts.MustRunAsTask.Default,
            ErrorText.Of(TaskTexts.MustRunAsTask.Key.Value));

    // A final failure whose description is the text's English default with the time filled in (stored on the item as it
    // is); the key and the time travel in the metadata for a renderer that translates it.
    private static Error Stopped(string code, TextDefinition text, string placeholder, TimeSpan time)
    {
        Dictionary<string, object?> arguments = new(StringComparer.Ordinal)
        {
            [placeholder] = time.ToString("c", CultureInfo.InvariantCulture),
        };

        return Error.Failure(
            code,
            TextPlaceholders.Format(text.Default, arguments, CultureInfo.InvariantCulture),
            ErrorText.Of(text.Key.Value, arguments));
    }
}
