using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Application.Tasks;

/// <summary>The scheduled-task texts, under <c>common.tasks.*</c>; collected by <c>TextSource.Common</c>.</summary>
public static class TaskTexts
{
    public static readonly TextCategory Category = TextCategory.Create("Common", "Tasks");

    private static readonly TextCategory errors = Category.Child("Errors");

    public static readonly TextDefinition Singular = Category.Text("Singular", "Scheduled task");

    /// <summary>The management validators: the template is not a <c>Schedulable()</c> task, or belongs to another module.</summary>
    public static readonly TextDefinition NotSchedulable = errors.Text("NotSchedulable", "This command cannot be scheduled.");

    /// <summary>More than one record picked for a command that does not run once per selected row.</summary>
    public static readonly TextDefinition TooManyTargets = errors.Text("TooManyTargets", "This command cannot be scheduled for that many records.");

    public static readonly TextDefinition NoTargets = errors.Text("NoTargets", "Pick at least one record.");

    /// <summary>The same record picked twice: a schedule holds each record once.</summary>
    public static readonly TextDefinition DuplicateTargets = errors.Text("DuplicateTargets", "Each record can be picked only once.");

    /// <summary>A target picked for a task that acts on one record, with no record id.</summary>
    public static readonly TextDefinition TargetWithoutId = errors.Text("TargetWithoutId", "Each picked record must have an id.");

    public static readonly TextDefinition InvalidCron = errors.Text("InvalidCron", "'{PropertyName}' is not a schedule the job engine can run.");

    public static readonly TextDefinition InvalidTimeZone = errors.Text("InvalidTimeZone", "'{PropertyName}' is not a known time zone.");

    /// <summary>An edit whose template is another command than the one the schedule was saved with.</summary>
    public static readonly TextDefinition CommandChanged = errors.Text("CommandChanged", "A saved schedule always sends the same command.");

    /// <summary>A task (<c>ITaskCommand</c>) sent inline instead of started as a run.</summary>
    public static readonly TextDefinition MustRunAsTask = errors.Text("MustRunAsTask", "This action runs as a task. Start it instead of sending it.");

    /// <summary>A run item stopped because its task reported no progress for its inactivity timeout.</summary>
    public static readonly TextDefinition TimedOut = errors.Text("TimedOut", "Timed out after {Timeout} without progress.");

    /// <summary>A run item stopped at its task's <c>MaxDuration</c>, however it reported.</summary>
    public static readonly TextDefinition MaxDurationExceeded = errors.Text("MaxDurationExceeded", "Stopped after the {MaxDuration} limit.");

    /// <summary>A record picked for a task over the whole collection (<c>ITaskCommand&lt;TRoot&gt;</c>), which takes none.</summary>
    public static readonly TextDefinition CollectionTargets = errors.Text("CollectionTargets", "This task works on the whole collection. Pick no record.");

    /// <summary>A start of a collection task while a run of it is still running in the same scope.</summary>
    public static readonly TextDefinition AlreadyRunning = errors.Text("AlreadyRunning", "{Task} is already running in {Scope}.");

    /// <summary>A cancel of a run that already finished.</summary>
    public static readonly TextDefinition RunFinished = errors.Text("RunFinished", "This run has already finished.");

    /// <summary>A cancel of a run whose task declares <c>NotCancelable()</c>.</summary>
    public static readonly TextDefinition NotCancelable = errors.Text("NotCancelable", "This task cannot be cancelled.");
}
