using NCrontab;
using TickerQ.Utilities;

namespace Sergin.SharedKernel.Infrastructure.ScheduledTasks.TickerQ;

/// <summary>
/// The cron rules both engines answer with, so the stand-in validates and previews a schedule exactly as TickerQ would
/// fire it once the licence arrives: TickerQ's own parser decides what is valid, NCrontab walks the next fire times.
/// </summary>
internal static class CronSchedule
{
    /// <summary>
    /// What TickerQ itself can fire: its own cron parser (five or six fields) and a zone the system knows. A
    /// <c>%placeholder%</c>, which TickerQ resolves from configuration, is not a schedule a person typed and is refused.
    /// </summary>
    public static bool IsValid(string cron, string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(cron) || cron.Contains('%', StringComparison.Ordinal) || string.IsNullOrWhiteSpace(timeZoneId))
        {
            return false;
        }

        if (!CronExpression.TryParse(cron, out _))
        {
            return false;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }

    /// <summary>
    /// The next fire times, from NCrontab, the parser TickerQ itself schedules with (six fields mean seconds first, as
    /// there). Gated on <see cref="IsValid"/>, so the two agree. NCrontab walks local clock time: each candidate is
    /// read in the zone, and a local time the zone skips is dropped.
    /// </summary>
    public static IReadOnlyList<DateTimeOffset> NextOccurrences(string cron, string timeZoneId, DateTimeOffset after, int count)
    {
        if (count <= 0 || !IsValid(cron, timeZoneId))
        {
            return [];
        }

        var options = new CrontabSchedule.ParseOptions { IncludingSeconds = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 6 };

        if (CrontabSchedule.TryParse(cron, options) is not { } schedule)
        {
            return [];
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        DateTime local = TimeZoneInfo.ConvertTime(after, zone).DateTime;
        List<DateTimeOffset> times = [];

        // A bounded walk: a schedule that never lands on a real local time (all in a DST gap) must not spin.
        for (int step = 0; step < count * 8 && times.Count < count; step++)
        {
            local = schedule.GetNextOccurrence(local);

            if (!zone.IsInvalidTime(local))
            {
                var next = new DateTimeOffset(local, zone.GetUtcOffset(local));

                if (next > after)
                {
                    times.Add(next);
                }
            }
        }

        return times;
    }
}
