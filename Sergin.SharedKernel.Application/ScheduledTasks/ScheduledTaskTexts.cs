using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>The scheduled-task texts, under <c>common.scheduled-tasks.*</c>; collected by <c>TextSource.Common</c>.</summary>
public static class ScheduledTaskTexts
{
    public static readonly TextCategory Category = TextCategory.Create("Common", "ScheduledTasks");

    private static readonly TextCategory errors = Category.Child("Errors");

    public static readonly TextDefinition Singular = Category.Text("Singular", "Scheduled task");

    /// <summary>The management validators: the template's command did not opt in with <c>Schedulable(name)</c>, or belongs to another module.</summary>
    public static readonly TextDefinition NotSchedulable = errors.Text("NotSchedulable", "This command cannot be scheduled.");

    /// <summary>More than one record picked for a command that does not run once per selected row.</summary>
    public static readonly TextDefinition TooManyTargets = errors.Text("TooManyTargets", "This command cannot be scheduled for that many records.");

    public static readonly TextDefinition NoTargets = errors.Text("NoTargets", "Pick at least one record.");

    /// <summary>The same record picked twice: a schedule holds each record once.</summary>
    public static readonly TextDefinition DuplicateTargets = errors.Text("DuplicateTargets", "Each record can be picked only once.");

    /// <summary>A child-entity command's target that does not name its parent record.</summary>
    public static readonly TextDefinition TargetWithoutRoot = errors.Text("TargetWithoutRoot", "Each picked record must name the record it belongs to.");

    public static readonly TextDefinition InvalidCron = errors.Text("InvalidCron", "'{PropertyName}' is not a schedule the job engine can run.");

    public static readonly TextDefinition InvalidTimeZone = errors.Text("InvalidTimeZone", "'{PropertyName}' is not a known time zone.");

    /// <summary>An edit whose template is another command than the one the schedule was saved with.</summary>
    public static readonly TextDefinition CommandChanged = errors.Text("CommandChanged", "A saved schedule always sends the same command.");
}
