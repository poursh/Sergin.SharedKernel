using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Sergin.SharedKernel.Presentation.Blazor.ScheduledTasks;

/// <summary>
/// The next fire times of a cron expression, worked out for the preview under the cron picker and the section's
/// Next run column. It reads the common shape — five fields (minute, hour, day of month, month, day of week), or six
/// with seconds first — made of <c>*</c>, numbers, lists, ranges and steps, with month and weekday names; anything
/// else (<c>L</c>, <c>W</c>, <c>#</c>, <c>%name%</c>) yields no preview rather than a wrong one. The job engine remains the authority on
/// whether an expression is valid and when it fires; this only shows a person what they typed.
/// </summary>
internal static class CronSchedule
{
    private const int HorizonDays = 1500;

    private static readonly string[] MonthNames = ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];
    private static readonly string[] DayNames = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];

    /// <summary>
    /// The first <paramref name="count"/> fire times after <paramref name="after"/>, in <paramref name="zone"/>'s own
    /// local time (a time the zone skips is left out, a repeated one fires once). Empty when the expression is not one
    /// this reads.
    /// </summary>
    public static IReadOnlyList<DateTimeOffset> NextRuns(string cron, TimeZoneInfo zone, DateTimeOffset after, int count)
    {
        ArgumentNullException.ThrowIfNull(zone);

        if (count <= 0 || !TryParse(cron, out Fields? fields))
        {
            return [];
        }

        List<DateTimeOffset> runs = [];
        DateTime day = TimeZoneInfo.ConvertTime(after, zone).Date;

        for (int offset = 0; offset < HorizonDays && runs.Count < count; offset++)
        {
            DateTime date = day.AddDays(offset);

            if (!fields.MatchesDate(date))
            {
                continue;
            }

            foreach (TimeSpan time in fields.TimesOfDay())
            {
                var local = DateTime.SpecifyKind(date + time, DateTimeKind.Unspecified);

                if (zone.IsInvalidTime(local))
                {
                    continue;
                }

                // A repeated local time fires at its first (daylight) offset.
                TimeSpan utcOffset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
                var run = new DateTimeOffset(local, utcOffset);

                if (run > after)
                {
                    runs.Add(run);

                    if (runs.Count == count)
                    {
                        break;
                    }
                }
            }
        }

        return runs;
    }

    private static bool TryParse(string? cron, [NotNullWhen(true)] out Fields? fields)
    {
        fields = null;

        string[] parts = cron?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

        if (parts.Length is not (5 or 6))
        {
            return false;
        }

        string[] rest = parts.Length == 6 ? parts[1..] : parts;
        SortedSet<int>? seconds = parts.Length == 6 ? Field(parts[0], 0, 59, null) : new SortedSet<int> { 0 };
        SortedSet<int>? minutes = Field(rest[0], 0, 59, null);
        SortedSet<int>? hours = Field(rest[1], 0, 23, null);
        SortedSet<int>? monthDays = Field(rest[2], 1, 31, null);
        SortedSet<int>? months = Field(rest[3], 1, 12, MonthNames, firstName: 1);
        SortedSet<int>? weekDays = Field(rest[4], 0, 7, DayNames, firstName: 0);

        if (seconds is null || minutes is null || hours is null || monthDays is null || months is null || weekDays is null)
        {
            return false;
        }

        // 7 is Sunday too.
        if (weekDays.Remove(7))
        {
            weekDays.Add(0);
        }

        fields = new Fields(seconds, minutes, hours, monthDays, months, weekDays, rest[2] == "*" || rest[2] == "?", rest[4] == "*" || rest[4] == "?");
        return true;
    }

    // One field as the set of values it allows, or null when it is not in the shape this reads.
    private static SortedSet<int>? Field(string text, int min, int max, string[]? names, int firstName = 0)
    {
        SortedSet<int> values = [];

        foreach (string item in text.Split(',', StringSplitOptions.TrimEntries))
        {
            if (!TryItem(item, min, max, names, firstName, values))
            {
                return null;
            }
        }

        return values.Count == 0 ? null : values;
    }

    private static bool TryItem(string item, int min, int max, string[]? names, int firstName, SortedSet<int> values)
    {
        string[] stepped = item.Split('/');
        int step = 1;

        if (stepped.Length > 2)
        {
            return false;
        }

        if (stepped.Length == 2 && !int.TryParse(stepped[1], NumberStyles.None, CultureInfo.InvariantCulture, out step))
        {
            return false;
        }

        if (step < 1)
        {
            return false;
        }

        int from;
        int to;

        if (stepped[0] is "*" or "?")
        {
            (from, to) = (min, max);
        }
        else
        {
            string[] range = stepped[0].Split('-');

            if (range.Length > 2 || !TryValue(range[0], names, firstName, out from))
            {
                return false;
            }

            to = from;

            if (range.Length == 2 && !TryValue(range[1], names, firstName, out to))
            {
                return false;
            }

            // "5/15" runs from 5 to the end of the range.
            if (range.Length == 1 && stepped.Length == 2)
            {
                to = max;
            }
        }

        if (from < min || to > max || from > to)
        {
            return false;
        }

        for (int value = from; value <= to; value += step)
        {
            values.Add(value);
        }

        return true;
    }

    private static bool TryValue(string text, string[]? names, int firstName, out int value)
    {
        int named = names is null ? -1 : Array.FindIndex(names, name => string.Equals(name, text, StringComparison.OrdinalIgnoreCase));

        if (named >= 0)
        {
            value = named + firstName;
            return true;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private sealed class Fields(
        SortedSet<int> seconds,
        SortedSet<int> minutes,
        SortedSet<int> hours,
        SortedSet<int> monthDays,
        SortedSet<int> months,
        SortedSet<int> weekDays,
        bool anyMonthDay,
        bool anyWeekDay)
    {
        /// <summary>Cron's rule: when both the day of month and the day of week are restricted, either one matches.</summary>
        public bool MatchesDate(DateTime date)
        {
            if (!months.Contains(date.Month))
            {
                return false;
            }

            bool monthDay = monthDays.Contains(date.Day);
            bool weekDay = weekDays.Contains((int)date.DayOfWeek);

            return anyMonthDay || anyWeekDay ? monthDay && weekDay : monthDay || weekDay;
        }

        public IEnumerable<TimeSpan> TimesOfDay() =>
            from hour in hours
            from minute in minutes
            from second in seconds
            select new TimeSpan(hour, minute, second);
    }
}
