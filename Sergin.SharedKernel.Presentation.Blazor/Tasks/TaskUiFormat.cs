using Sergin.SharedKernel.Application.Tasks.Queries;
using Sergin.SharedKernel.Domain.Tasks;
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
            TaskRunTrigger.Manual => TaskUiTexts.Runs.Background,
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
        var values = new
        {
            Status = localizer.Text(StatusText(run.Status)),
            Succeeded = formatter.Number(run.Succeeded),
            Total = formatter.Number(run.Total),
            Failed = formatter.Number(run.Failed),
            Cancelled = formatter.Number(run.Cancelled),
        };

        return localizer.Text(run.Cancelled > 0 ? TaskUiTexts.Runs.SummaryWithCancelled : TaskUiTexts.Runs.Summary, values);
    }
}
