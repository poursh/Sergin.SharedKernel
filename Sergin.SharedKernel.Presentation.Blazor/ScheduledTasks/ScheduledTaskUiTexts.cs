using Sergin.SharedKernel.Application.ScheduledTasks;

namespace Sergin.SharedKernel.Presentation.Blazor.ScheduledTasks;

/// <summary>
/// The words of the scheduled-task parts — the schedule action and dialog, the cron picker, the section and the runs
/// dialog — under <c>common.scheduled-tasks.*</c>, beside the Application-side <see cref="ScheduledTaskTexts"/> (same
/// category, so one key space). Only these parts show them, so they are declared here, not in Application.
/// </summary>
public static class ScheduledTaskUiTexts
{
    private static readonly TextCategory category = ScheduledTaskTexts.Category;

    public static TextDefinition Title { get; } = category.Text("Title", "Scheduled tasks");

    public static TextDefinition ViewSchedules { get; } = category.Text("ViewSchedules", "View schedules ({Count})");

    public static TextDefinition FilteredTo { get; } = category.Text("FilteredTo", "Showing {Command}");

    public static TextDefinition ScheduleAction { get; } = category.Text("ScheduleAction", "Schedule…");

    public static TextDefinition RunNow { get; } = category.Text("RunNow", "Run now");

    public static TextDefinition Save { get; } = category.Text("Save", "Save");

    public static TextDefinition RunOnceStarted { get; } = category.Text("RunOnceStarted", "The run has started.");

    public static TextDefinition WasSaved { get; } = category.Text("Saved", "The schedule was saved.");

    public static TextDefinition WasPaused { get; } = category.Text("Paused", "The schedule was paused.");

    public static TextDefinition WasResumed { get; } = category.Text("Resumed", "The schedule was resumed.");

    public static TextDefinition WasDeleted { get; } = category.Text("Deleted", "The schedule was deleted.");

    public static TextDefinition Pause { get; } = category.Text("Pause", "Pause");

    public static TextDefinition Resume { get; } = category.Text("Resume", "Resume");

    public static TextDefinition DeleteTitle { get; } = category.Text("DeleteTitle", "Delete schedule");

    public static TextDefinition DeleteConfirm { get; } = category.Text("DeleteConfirm", "Delete the schedule for {Command}?");

    public static TextDefinition EditTitle { get; } = category.Text("EditTitle", "Edit schedule");

    public static TextDefinition Empty { get; } = category.Text("Empty", "Nothing is scheduled for this record.");

    public static TextDefinition OneOffRuns { get; } = category.Text("OneOffRuns", "Recent one-off runs");

    public static TextDefinition OneOffEmpty { get; } = category.Text("OneOffEmpty", "Nothing has been run once here yet.");

    public static TextDefinition Never { get; } = category.Text("Never", "Never run");

    public static TextDefinition NextRunUnavailable { get; } = category.Text("NextRunUnavailable", "Not available");

    public static TextDefinition StaleReloaded { get; } =
        category.Text("StaleReloaded", "Someone else changed this schedule. It was reloaded: check it and save again.");

    public static TextDefinition NotScheduled { get; } = category.Text("NotScheduled", "Not scheduled");

    /// <summary>Shown by the section and the dialog while the host's engine fires no trigger (the stand-in, before a licence).</summary>
    public static TextDefinition EngineOff { get; } =
        category.Text("EngineOff", "Scheduled runs are off until the scheduling engine is configured. Run now still works.");

    /// <summary>The dialog's own captions.</summary>
    public static class Dialog
    {
        private static readonly TextCategory dialog = category.Child("Dialog");

        public static TextDefinition Targets { get; } = dialog.Text("Targets", "Records");

        public static TextDefinition Trigger { get; } = dialog.Text("Trigger", "Schedule");
    }

    /// <summary>The table's column headings.</summary>
    public static class Columns
    {
        private static readonly TextCategory columns = category.Child("Columns");

        public static TextDefinition Command { get; } = columns.Text("Command", "Command");

        public static TextDefinition Schedule { get; } = columns.Text("Schedule", "Schedule");

        public static TextDefinition NextRun { get; } = columns.Text("NextRun", "Next run");

        public static TextDefinition LastOutcome { get; } = columns.Text("LastOutcome", "Last outcome");

        public static TextDefinition State { get; } = columns.Text("State", "State");
    }

    /// <summary>A schedule's state.</summary>
    public static class States
    {
        private static readonly TextCategory states = category.Child("States");

        public static TextDefinition Active { get; } = states.Text("Active", "Active");

        public static TextDefinition Paused { get; } = states.Text("Paused", "Paused");
    }

    /// <summary>The runs dialog, and the words for a run's trigger and status and a run item's status.</summary>
    public static class Runs
    {
        private static readonly TextCategory runs = category.Child("Runs");

        public static TextDefinition Heading { get; } = runs.Text("Title", "Runs · {Command}");

        public static TextDefinition NoRuns { get; } = runs.Text("Empty", "No runs yet.");

        public static TextDefinition Started { get; } = runs.Text("Started", "Started");

        public static TextDefinition Trigger { get; } = runs.Text("Trigger", "Trigger");

        public static TextDefinition ActedBy { get; } = runs.Text("ActedBy", "Run by");

        public static TextDefinition Status { get; } = runs.Text("Status", "Status");

        public static TextDefinition Summary { get; } = runs.Text("Summary", "{Status}: {Succeeded} of {Total} succeeded, {Failed} failed");

        public static TextDefinition LastOutcome { get; } = runs.Text("LastOutcome", "{Status}, {When}");

        public static TextDefinition LastOutcomeWithFailures { get; } = runs.Text("LastOutcomeWithFailures", "{Status}, {When}: {Failed} failed");

        public static TextDefinition Items { get; } = runs.Text("Items", "Records of this run");

        public static TextDefinition Target { get; } = runs.Text("Target", "Record");

        public static TextDefinition Attempts { get; } = runs.Text("Attempts", "Attempts");

        public static TextDefinition Error { get; } = runs.Text("Error", "Error");

        public static TextDefinition ShowItems { get; } = runs.Text("ShowItems", "Show records");

        public static TextDefinition Scheduled { get; } = runs.Text("Scheduled", "Scheduled");

        public static TextDefinition RunNowTrigger { get; } = runs.Text("RunNowTrigger", "Run now");

        public static TextDefinition OneOff { get; } = runs.Text("OneOff", "One-off");

        public static TextDefinition Running { get; } = runs.Text("Running", "Running");

        public static TextDefinition Completed { get; } = runs.Text("Completed", "Completed");

        public static TextDefinition Skipped { get; } = runs.Text("Skipped", "Skipped");

        public static TextDefinition ItemPending { get; } = runs.Text("ItemPending", "Pending");

        public static TextDefinition ItemRunning { get; } = runs.Text("ItemRunning", "Running");

        public static TextDefinition ItemRetrying { get; } = runs.Text("ItemRetrying", "Retrying");

        public static TextDefinition ItemSucceeded { get; } = runs.Text("ItemSucceeded", "Succeeded");

        public static TextDefinition ItemFailed { get; } = runs.Text("ItemFailed", "Failed");
    }

    /// <summary>The cron picker.</summary>
    public static class Cron
    {
        private static readonly TextCategory cron = category.Child("Cron");

        public static TextDefinition Daily { get; } = cron.Text("Daily", "Daily at");

        public static TextDefinition Hourly { get; } = cron.Text("Hourly", "Hourly at");

        public static TextDefinition Weekly { get; } = cron.Text("Weekly", "Weekly on");

        public static TextDefinition Custom { get; } = cron.Text("Custom", "Custom (cron expression)");

        public static TextDefinition NextRuns { get; } = cron.Text("NextRuns", "Next runs");

        public static TextDefinition NoNextRuns { get; } = cron.Text("NoNextRuns", "No upcoming run could be worked out.");

        public static TextDefinition Time { get; } = cron.Text("Time", "Time");

        public static TextDefinition Minute { get; } = cron.Text("Minute", "Minute");

        public static TextDefinition Day { get; } = cron.Text("Day", "Day");

        public static TextDefinition Expression { get; } = cron.Text("Expression", "Cron expression");

        public static TextDefinition TimeZone { get; } = cron.Text("TimeZone", "Time zone: {Zone}");

        public static TextDefinition Repeat { get; } = cron.Text("Repeat", "Repeat");
    }
}
