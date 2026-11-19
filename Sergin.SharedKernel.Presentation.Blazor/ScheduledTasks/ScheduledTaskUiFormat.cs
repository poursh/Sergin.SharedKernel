using Sergin.SharedKernel.Application.ScheduledTasks.Queries;
using Sergin.SharedKernel.Domain.ScheduledTasks;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.ScheduledTasks;

/// <summary>What the section and the runs dialog both say about runs, times and people: one place, so they cannot drift.</summary>
internal static class ScheduledTaskUiFormat
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

    public static TextDefinition StatusText(ScheduledRunStatus status) =>
        status switch
        {
            ScheduledRunStatus.Running => ScheduledTaskUiTexts.Runs.Running,
            ScheduledRunStatus.Completed => ScheduledTaskUiTexts.Runs.Completed,
            _ => ScheduledTaskUiTexts.Runs.Skipped,
        };

    public static TextDefinition TriggerText(ScheduledRunTrigger trigger) =>
        trigger switch
        {
            ScheduledRunTrigger.Scheduled => ScheduledTaskUiTexts.Runs.Scheduled,
            ScheduledRunTrigger.RunNow => ScheduledTaskUiTexts.Runs.RunNowTrigger,
            ScheduledRunTrigger.Background => ScheduledTaskUiTexts.Runs.Background,
            _ => ScheduledTaskUiTexts.Runs.OneOff,
        };

    public static TextDefinition ItemStatusText(ScheduledRunItemStatus status) =>
        status switch
        {
            ScheduledRunItemStatus.Pending => ScheduledTaskUiTexts.Runs.ItemPending,
            ScheduledRunItemStatus.Running => ScheduledTaskUiTexts.Runs.ItemRunning,
            ScheduledRunItemStatus.Retrying => ScheduledTaskUiTexts.Runs.ItemRetrying,
            ScheduledRunItemStatus.Succeeded => ScheduledTaskUiTexts.Runs.ItemSucceeded,
            ScheduledRunItemStatus.Cancelled => ScheduledTaskUiTexts.Runs.ItemCancelled,
            _ => ScheduledTaskUiTexts.Runs.ItemFailed,
        };

    /// <summary>
    /// An item's status; a Running item that has reported adds its percent.
    /// </summary>
    public static string ItemStatus(IStringLocalizer localizer, ICultureFormatter formatter, ScheduledRunItemListItem item)
    {
        string status = localizer.Text(ItemStatusText(item.Status));

        return item is { Status: ScheduledRunItemStatus.Running, Progress: { } percent }
            ? localizer.Text(ScheduledTaskUiTexts.Runs.ItemProgress, new { Status = status, Percent = formatter.Number(percent) })
            : status;
    }

    /// <summary>
    /// One sentence for a run: its status and how many records succeeded and failed, and how many were cancelled when
    /// any were.
    /// </summary>
    public static string RunSummary(IStringLocalizer localizer, ICultureFormatter formatter, ScheduledRunListItem run)
    {
        var values = new
        {
            Status = localizer.Text(StatusText(run.Status)),
            Succeeded = formatter.Number(run.Succeeded),
            Total = formatter.Number(run.Total),
            Failed = formatter.Number(run.Failed),
            Cancelled = formatter.Number(run.Cancelled),
        };

        return localizer.Text(run.Cancelled > 0 ? ScheduledTaskUiTexts.Runs.SummaryWithCancelled : ScheduledTaskUiTexts.Runs.Summary, values);
    }
}
