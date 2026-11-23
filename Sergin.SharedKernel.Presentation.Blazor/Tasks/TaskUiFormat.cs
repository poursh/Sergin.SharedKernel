using Sergin.SharedKernel.Application.Tasks.Queries;
using Sergin.SharedKernel.Domain.Tasks;
using Sergin.SharedKernel.Presentation.Blazor.Scopes;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Tasks;

/// <summary>What the section and the runs dialog both say about runs, times and people: one place, so they cannot drift.</summary>
internal static class TaskUiFormat
{
    public static TimeZoneInfo ZoneOf(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    public static string Time(ICultureFormatter formatter, TimeZoneInfo zone, DateTime utc) =>
        formatter.DateTime(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)), zone);

    public static string NameOf(IReadOnlyDictionary<Guid, string> names, Guid userId) =>
        names.GetValueOrDefault(userId) ?? userId.ToString("N")[..8];

    /// <summary>
    /// What names the scope a collection task runs in, which has no record to name it by: its name among the scopes the
    /// circuit lists (user data, as stored), else its short id.
    /// </summary>
    public static string ScopeName(SerginScopeSession scopes, Guid scopeId) =>
        scopes.Options.FirstOrDefault(option => option.Id == scopeId)?.Name ?? scopeId.ToString("N")[..8];

    public static TextDefinition StatusText(TaskRunStatus status) =>
        status switch
        {
            TaskRunStatus.Running => TaskUiTexts.Runs.Running,
            TaskRunStatus.Completed => TaskUiTexts.Runs.Completed,
            _ => TaskUiTexts.Runs.Skipped,
        };

    public static TextDefinition TriggerText(TaskRunTrigger trigger) =>
        trigger switch
        {
            TaskRunTrigger.Schedule => TaskUiTexts.Runs.Scheduled,
            TaskRunTrigger.ScheduleNow => TaskUiTexts.Runs.RunNowTrigger,
            TaskRunTrigger.Manual => TaskUiTexts.Runs.Manual,
            _ => TaskUiTexts.Runs.OneOff,
        };

    public static TextDefinition ItemStatusText(TaskRunItemStatus status) =>
        status switch
        {
            TaskRunItemStatus.Pending => TaskUiTexts.Runs.ItemPending,
            TaskRunItemStatus.Running => TaskUiTexts.Runs.ItemRunning,
            TaskRunItemStatus.Retrying => TaskUiTexts.Runs.ItemRetrying,
            TaskRunItemStatus.Succeeded => TaskUiTexts.Runs.ItemSucceeded,
            TaskRunItemStatus.Cancelled => TaskUiTexts.Runs.ItemCancelled,
            _ => TaskUiTexts.Runs.ItemFailed,
        };

    /// <summary>
    /// An item's status; a Running item that has reported adds its percent.
    /// </summary>
    public static string ItemStatus(IStringLocalizer localizer, ICultureFormatter formatter, TaskRunItemListItem item)
    {
        string status = localizer.Text(ItemStatusText(item.Status));

        return item is { Status: TaskRunItemStatus.Running, Progress: { } percent }
            ? localizer.Text(TaskUiTexts.Runs.ItemProgress, new { Status = status, Percent = formatter.Number(percent) })
            : status;
    }

    /// <summary>
    /// One sentence for a run: its status and how many records succeeded and failed, and how many were cancelled when
    /// any were.
    /// </summary>
    public static string RunSummary(IStringLocalizer localizer, ICultureFormatter formatter, TaskRunListItem run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return RunSummary(localizer, formatter, StatusText(run.Status), new RunCounts(run.Total, run.Succeeded, run.Failed, run.Cancelled));
    }

    /// <summary>The same sentence from a status text and the run's counts, for a list that reads no stored status.</summary>
    public static string RunSummary(IStringLocalizer localizer, ICultureFormatter formatter, TextDefinition status, RunCounts counts)
    {
        var values = new
        {
            Status = localizer.Text(status),
            Succeeded = formatter.Number(counts.Succeeded),
            Total = formatter.Number(counts.Total),
            Failed = formatter.Number(counts.Failed),
            Cancelled = formatter.Number(counts.Cancelled),
        };

        return localizer.Text(counts.Cancelled > 0 ? TaskUiTexts.Runs.SummaryWithCancelled : TaskUiTexts.Runs.Summary, values);
    }
}

/// <summary>A finished run's item counts, as its summary reads them.</summary>
internal readonly record struct RunCounts(int Total, int Succeeded, int Failed, int Cancelled);
