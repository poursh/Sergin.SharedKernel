using Sergin.SharedKernel.Application.Tasks;

namespace Sergin.SharedKernel.Presentation.Blazor.Tasks;

/// <summary>
/// The words of the task parts — the schedule action and dialog, the cron picker, the section and the runs
/// dialog — under <c>common.tasks.*</c>, beside the Application-side <see cref="TaskTexts"/> (same
/// category, so one key space). Only these parts show them, so they are declared here, not in Application.
/// </summary>
public static class TaskUiTexts
{
    private static readonly TextCategory category = TaskTexts.Category;

    public static TextDefinition Title { get; } = category.Text("Title", "Scheduled tasks");

    public static TextDefinition ViewSchedules { get; } = category.Text("ViewSchedules", "View schedules ({Count})");

    /// <summary>A collection task's list-header entry that opens its schedules in a dialog.</summary>
    public static TextDefinition Schedules { get; } = category.Text("Schedules", "Schedules");

    /// <summary>The schedules dialog's title: the collection task and the scope its schedules are listed in.</summary>
    public static TextDefinition SchedulesIn { get; } = category.Text("SchedulesIn", "{Task} · {Scope}");

    public static TextDefinition FilteredTo { get; } = category.Text("FilteredTo", "Showing {Command}");

    public static TextDefinition ScheduleAction { get; } = category.Text("ScheduleAction", "Schedule…");

    public static TextDefinition RunNow { get; } = category.Text("RunNow", "Run now");

    /// <summary>A task's argument form's submit: starts the task with the values entered.</summary>
    public static TextDefinition Start { get; } = category.Text("Start", "Start");

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

    public static TextDefinition EmptyInScope { get; } = category.Text("EmptyInScope", "Nothing is scheduled in this scope.");

    public static TextDefinition RecentRuns { get; } = category.Text("RecentRuns", "Recent runs");

    public static TextDefinition RecentRunsEmpty { get; } = category.Text("RecentRunsEmpty", "Nothing has run here yet.");

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

        /// <summary>The caption above a collection task's scope, which it runs in instead of on records.</summary>
        public static TextDefinition Scope { get; } = dialog.Text("Scope", "Scope");

        public static TextDefinition Trigger { get; } = dialog.Text("Trigger", "Schedule");

        /// <summary>The dialog's title over one record: the command, then the record count.</summary>
        public static TextDefinition TitleOneRecord { get; } = dialog.Text("TitleOneRecord", "{Command} · 1 record");

        /// <summary>The dialog's title over several records: the command, then how many.</summary>
        public static TextDefinition TitleRecords { get; } = dialog.Text("TitleRecords", "{Command} · {Count} records");

        /// <summary>A collection task's dialog title: the command, then the scope it runs in.</summary>
        public static TextDefinition TitleInScope { get; } = dialog.Text("TitleInScope", "{Command} · {Scope}");
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

        public static TextDefinition Manual { get; } = runs.Text("Manual", "Manual");

        public static TextDefinition Running { get; } = runs.Text("Running", "Running");

        public static TextDefinition Completed { get; } = runs.Text("Completed", "Completed");

        public static TextDefinition Skipped { get; } = runs.Text("Skipped", "Skipped");

        public static TextDefinition ItemPending { get; } = runs.Text("ItemPending", "Pending");

        public static TextDefinition ItemRunning { get; } = runs.Text("ItemRunning", "Running");

        public static TextDefinition ItemRetrying { get; } = runs.Text("ItemRetrying", "Retrying");

        public static TextDefinition ItemSucceeded { get; } = runs.Text("ItemSucceeded", "Succeeded");

        public static TextDefinition ItemFailed { get; } = runs.Text("ItemFailed", "Failed");

        public static TextDefinition ItemCancelled { get; } = runs.Text("ItemCancelled", "Cancelled");

        /// <summary>A running item's status with the percent its command last reported.</summary>
        public static TextDefinition ItemProgress { get; } = runs.Text("ItemProgress", "{Status} · {Percent}%");

        /// <summary>A run's summary when some of its items were cancelled.</summary>
        public static TextDefinition SummaryWithCancelled { get; } = runs.Text(
            "SummaryWithCancelled", "{Status}: {Succeeded} of {Total} succeeded, {Failed} failed, {Cancelled} cancelled");
    }

    /// <summary>The app bar's activity indicator: the user's running and recently finished background runs.</summary>
    public static class Activity
    {
        private static readonly TextCategory activity = category.Child("Activity");

        public static TextDefinition Heading { get; } = activity.Text("Title", "Activity");

        public static TextDefinition Running { get; } = activity.Text("Running", "Running");

        public static TextDefinition Finished { get; } = activity.Text("Finished", "Finished in the last 24 hours");

        public static TextDefinition NothingRunning { get; } = activity.Text("Empty", "Nothing is running.");

        public static TextDefinition Records { get; } = activity.Text("Records", "{Count} records");

        public static TextDefinition OneRecord { get; } = activity.Text("OneRecord", "1 record");

        public static TextDefinition Clear { get; } = activity.Text("Clear", "Clear");

        public static TextDefinition Show { get; } = activity.Text("Show", "Show");

        public static TextDefinition FinishedSucceeded { get; } = activity.Text("FinishedSucceeded", "{Command} finished for {Target}.");

        public static TextDefinition FinishedFailed { get; } = activity.Text("FinishedFailed", "{Command} failed for {Target}.");

        public static TextDefinition FinishedPartly { get; } =
            activity.Text("FinishedPartly", "{Command} finished for {Done} of {Total} records.");

        /// <summary>A run cancelled part way through with no failure: what was done before the cancel, and what was not.</summary>
        public static TextDefinition FinishedPartlyCancelled { get; } = activity.Text(
            "FinishedPartlyCancelled", "{Command} finished for {Done} of {Total} records; {Cancelled} cancelled.");

        /// <summary>A running run's records done so far, under its progress bar.</summary>
        public static TextDefinition Progress { get; } = activity.Text("Progress", "{Done} / {Total}");

        public static TextDefinition OpenLabel { get; } = activity.Text("OpenLabel", "Activity, {Count} running");

        public static TextDefinition Cancel { get; } = activity.Text("Cancel", "Cancel");

        public static TextDefinition Cancelled { get; } = activity.Text("Cancelled", "Cancelled");

        public static TextDefinition NotCancelable { get; } = activity.Text("NotCancelable", "Can't be cancelled");

        public static TextDefinition FinishedCancelled { get; } = activity.Text("FinishedCancelled", "{Command} was cancelled for {Target}.");

        public static TextDefinition CancelFailed { get; } = activity.Text("CancelFailed", "The run could not be cancelled.");
    }

    /// <summary>
    /// The Task runs page and its records dialog. The list's field labels and run outcomes are Application's
    /// (<see cref="TaskTexts.RunList"/>), on the same category under <c>fields.*</c> and <c>values.*</c>.
    /// </summary>
    public static class RunList
    {
        private static readonly TextCategory runList = category.Child("RunList");

        public static TextDefinition Heading { get; } = runList.Text("Title", "Task runs");

        /// <summary>The module selector's label, shown when more than one module runs tasks.</summary>
        public static TextDefinition Module { get; } = runList.Text("Module", "Module");

        /// <summary>The column naming what a run ran on: its record, its record count or its scope.</summary>
        public static TextDefinition Records { get; } = runList.Text("Records", "Records");

        public static TextDefinition NoModule { get; } = runList.Text("NoModule", "No module runs tasks in this application.");

        public static TextDefinition NoRuns { get; } = runList.Text("NoRuns", "No runs match.");

        /// <summary>A finished run's counts, under its outcome.</summary>
        public static TextDefinition Summary { get; } = runList.Text("Summary", "{Succeeded} of {Total} succeeded, {Failed} failed");

        /// <summary>A finished run's counts when some of its records were cancelled.</summary>
        public static TextDefinition SummaryWithCancelled { get; } =
            runList.Text("SummaryWithCancelled", "{Succeeded} of {Total} succeeded, {Failed} failed, {Cancelled} cancelled");

        /// <summary>The records dialog's column of when each record finished.</summary>
        public static TextDefinition FinishedAt { get; } = runList.Text("FinishedAt", "Finished");

        public static TextDefinition TriggerManual { get; } = runList.Text("TriggerManual", "Started");

        public static TextDefinition TriggerOnce { get; } = runList.Text("TriggerOnce", "Run once");

        public static TextDefinition TriggerScheduleNow { get; } = runList.Text("TriggerScheduleNow", "Run now");

        public static TextDefinition TriggerSchedule { get; } = runList.Text("TriggerSchedule", "Schedule");

        /// <summary>A schedule's run: the trigger, then the schedule's cron as stored (schedules have no name).</summary>
        public static TextDefinition TriggerWithSchedule { get; } = runList.Text("TriggerWithSchedule", "{Trigger} · {Schedule}");

        /// <summary>A filter picker with nothing chosen.</summary>
        public static TextDefinition AnyValue { get; } = runList.Text("AnyValue", "Any");
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
