using System.Globalization;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Presentation.Blazor.Times;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.ScheduledTasks;

/// <summary>The common shapes the picker offers; anything else is <see cref="Custom"/>, typed as cron text.</summary>
public enum CronChoice
{
    Daily,
    Hourly,
    Weekly,
    Custom,
}

/// <summary>
/// Picks when a schedule fires: daily at a time, hourly at a minute, weekly on a day at a time, or a cron expression of
/// one's own — shown with the time zone it runs in and the next three fire times. <see cref="Cron"/> and
/// <see cref="TimeZoneId"/> are two-way; the zone starts as the viewer's (<see cref="IUiTimeZoneStore"/>).
/// </summary>
/// <remarks>
/// The fire times are worked out here from the expression for a preview (<see cref="CronSchedule"/>); the job engine
/// decides what really fires, and the schedule's own validator refuses an expression it cannot run. A form that wants
/// the custom field validated as the user types passes <see cref="CronFor"/>, the expression of its model's cron property.
/// </remarks>
public sealed partial class SerginCronPicker
{
    private const int PreviewCount = 3;
    private const string TimeFormat = "HH:mm";
    private static readonly TimeOnly DefaultTime = new(2, 0);

    private CronChoice choice = CronChoice.Daily;
    private TimeOnly time = DefaultTime;
    private DayOfWeek day = DayOfWeek.Monday;
    private string custom = string.Empty;
    private string? lastCron;

    [Parameter]
    public string Cron { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> CronChanged { get; set; }

    [Parameter]
    public string TimeZoneId { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> TimeZoneIdChanged { get; set; }

    /// <summary>The cron property of the form's model, so the custom field shows the pipeline's rule for it.</summary>
    [Parameter]
    public Expression<Func<string>>? CronFor { get; set; }

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    [Inject]
    private ICultureFormatter Formatter { get; set; } = default!;

    [Inject]
    private IUiTimeZoneStore TimeZones { get; set; } = default!;

    [Inject]
    private IDateTimeProvider Clock { get; set; } = default!;

    private string TimeText
    {
        get => time.ToString(TimeFormat, CultureInfo.InvariantCulture);
        set
        {
            if (TimeOnly.TryParseExact(value, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly parsed))
            {
                time = parsed;
            }
        }
    }

    private TimeZoneInfo Zone => ZoneOf(TimeZoneId);

    private IReadOnlyList<string> NextRuns =>
    [
        .. CronSchedule.NextRuns(Cron, Zone, new DateTimeOffset(DateTime.SpecifyKind(Clock.UtcNow, DateTimeKind.Utc)), PreviewCount)
            .Select(run => Formatter.DateTime(run, Zone)),
    ];

    /// <summary>The cron expression for a choice; <paramref name="time"/> gives the hour and minute, <paramref name="custom"/> is used as typed.</summary>
    internal static string ToCron(CronChoice choice, TimeOnly time, DayOfWeek day = DayOfWeek.Monday, string custom = "") =>
        choice switch
        {
            CronChoice.Daily => $"{time.Minute} {time.Hour} * * *",
            CronChoice.Hourly => $"{time.Minute} * * * *",
            CronChoice.Weekly => $"{time.Minute} {time.Hour} * * {(int)day}",
            _ => custom,
        };

    /// <summary>Which choice an existing expression is, so editing a saved schedule opens on its own shape.</summary>
    internal static CronChoice ChoiceOf(string cron) => Read(cron).Choice;

    internal static (CronChoice Choice, TimeOnly Time, DayOfWeek Day) Read(string cron)
    {
        if (DailyShape().Match(cron) is { Success: true } daily)
        {
            return (CronChoice.Daily, TimeOfDay(daily.Groups[2].Value, daily.Groups[1].Value), DayOfWeek.Monday);
        }

        if (HourlyShape().Match(cron) is { Success: true } hourly)
        {
            return (CronChoice.Hourly, TimeOfDay("0", hourly.Groups[1].Value), DayOfWeek.Monday);
        }

        if (WeeklyShape().Match(cron) is { Success: true } weekly)
        {
            return (CronChoice.Weekly, TimeOfDay(weekly.Groups[2].Value, weekly.Groups[1].Value), (DayOfWeek)(int.Parse(weekly.Groups[3].Value, CultureInfo.InvariantCulture) % 7));
        }

        return (CronChoice.Custom, DefaultTime, DayOfWeek.Monday);
    }

    protected override async Task OnInitializedAsync()
    {
        if (string.IsNullOrWhiteSpace(Cron))
        {
            await EmitAsync();
        }

        if (string.IsNullOrWhiteSpace(TimeZoneId))
        {
            TimeZoneId = (await TimeZones.GetTimeZoneAsync()).Id;
            await TimeZoneIdChanged.InvokeAsync(TimeZoneId);
        }
    }

    // The parent may set Cron (an edit opening a saved schedule): follow it, without echoing it back.
    protected override void OnParametersSet()
    {
        if (string.IsNullOrWhiteSpace(Cron) || Cron == lastCron)
        {
            return;
        }

        (CronChoice choice, TimeOnly time, DayOfWeek day) shape = Read(Cron);

        choice = shape.choice;

        if (shape.choice == CronChoice.Custom)
        {
            custom = Cron;
        }
        else
        {
            time = shape.time;
            day = shape.day;
        }

        lastCron = Cron;
    }

    private static TimeZoneInfo ZoneOf(string id)
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

    private static TimeOnly TimeOfDay(string hour, string minute) =>
        new(int.Parse(hour, CultureInfo.InvariantCulture) % 24, int.Parse(minute, CultureInfo.InvariantCulture) % 60);

    private string DayName(DayOfWeek value) => Formatter.Culture.DateTimeFormat.GetDayName(value);

    private Task ChoiceChangedAsync(CronChoice value)
    {
        choice = value;
        return EmitAsync();
    }

    private Task TimeChangedAsync(string? value)
    {
        TimeText = value ?? string.Empty;
        return EmitAsync();
    }

    private Task MinuteChangedAsync(int value)
    {
        time = new TimeOnly(time.Hour, Math.Clamp(value, 0, 59));
        return EmitAsync();
    }

    private Task DayChangedAsync(DayOfWeek value)
    {
        day = value;
        return EmitAsync();
    }

    private Task CustomChangedAsync(string? value)
    {
        custom = value ?? string.Empty;
        return EmitAsync();
    }

    private Task EmitAsync()
    {
        Cron = ToCron(choice, time, day, custom.Trim());
        lastCron = Cron;

        return CronChanged.InvokeAsync(Cron);
    }

    [GeneratedRegex(@"^(\d{1,2}) (\d{1,2}) \* \* \*$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DailyShape();

    [GeneratedRegex(@"^(\d{1,2}) \* \* \* \*$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HourlyShape();

    [GeneratedRegex(@"^(\d{1,2}) (\d{1,2}) \* \* ([0-7])$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WeeklyShape();
}
