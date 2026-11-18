using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using MudBlazor;
using Sergin.SharedKernel.Application.ScheduledTasks;
using Sergin.SharedKernel.Application.ScheduledTasks.Activity;
using Sergin.SharedKernel.Application.ScheduledTasks.Commands;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.TimeZones;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.ScheduledTasks;

/// <summary>
/// The app bar's activity indicator: the signed-in user's own background runs from every module, running or finished in
/// the last 24 hours. It loads after the first interactive render, then reloads whenever <see cref="IRunNotifier"/> says
/// one of the user's runs changed, and every 5 seconds while a run is unfinished (a run executed by another process
/// publishes nothing here). It never breaks the shell: an anonymous viewer and a failed load render nothing.
/// </summary>
public sealed partial class SerginActivityIndicator : IDisposable
{
    private const string ClearedKeyPrefix = "sergin.activity.cleared.";

    private static readonly TimeSpan window = TimeSpan.FromHours(24);
    private static readonly TimeSpan pollInterval = TimeSpan.FromSeconds(5);

    private IReadOnlyList<ActivityRow> rows = [];
    private TimeZoneInfo timeZone = TimeZoneInfo.Utc;
    private DateTime? clearedAtUtc;
    private IDisposable? subscription;
    private PeriodicTimer? poller;
    private bool loaded;
    private bool started;
    private bool open;
    private volatile bool disposed;
    private int polling;

    [Inject]
    private ISerginDispatcher Dispatcher { get; set; } = default!;

    [Inject]
    private IRunNotifier Notifier { get; set; } = default!;

    [Inject]
    private IUserContext User { get; set; } = default!;

    [Inject]
    private ScheduledCommandCatalog Catalog { get; set; } = default!;

    [Inject]
    private AggregateViewRegistry Views { get; set; } = default!;

    [Inject]
    private IDateTimeProvider Clock { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    [Inject]
    private ICultureFormatter Formatter { get; set; } = default!;

    [Inject]
    private IUiTimeZoneStore TimeZones { get; set; } = default!;

    [Inject]
    private IOptions<SerginTimeZoneOptions> TimeZoneOptions { get; set; } = default!;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private ActivityPanelState Panel { get; set; } = default!;

    [Inject]
    private ILogger<SerginActivityIndicator> Logger { get; set; } = default!;

    /// <summary>Whether the indicator hears the user's run changes (from the first load until disposal).</summary>
    internal bool IsSubscribed => subscription is not null;

    /// <summary>Whether the fallback poll runs (while a listed run is unfinished, never after disposal).</summary>
    internal bool IsPolling => poller is not null;

    // The shape every anonymous context has: ClaimsPrincipalUserContext.Anonymous and the API's stub alike carry no id.
    private bool IsAnonymous => User.Id.Value == Guid.Empty;

    private bool Visible => loaded && !IsAnonymous;

    private int RunningCount => rows.Count(row => row.IsRunning);

    private IReadOnlyList<ActivityRow> Running => [.. rows.Where(row => row.IsRunning)];

    // Finished rows the user cleared stay hidden: those that finished at or before the stored watermark.
    private IReadOnlyList<ActivityRow> Finished =>
        [.. rows.Where(row => !row.IsRunning && (clearedAtUtc is not { } cleared || row.CompletedAtUtc is not { } done || done > cleared))];

    private IReadOnlyList<(TextDefinition Heading, IReadOnlyList<ActivityRow> Rows)> Sections
    {
        get
        {
            IReadOnlyList<ActivityRow> finished = Finished;

            return finished.Count == 0
                ? [(ScheduledTaskUiTexts.Activity.Running, Running)]
                : [(ScheduledTaskUiTexts.Activity.Running, Running), (ScheduledTaskUiTexts.Activity.Finished, finished)];
        }
    }

    private string OpenLabel => Localizer.Text(ScheduledTaskUiTexts.Activity.OpenLabel, new { Count = Formatter.Number(RunningCount) });

    private string ClearedKey => ClearedKeyPrefix + User.Id.Value.ToString("D", CultureInfo.InvariantCulture);

    protected override void OnInitialized() => Panel.OpenRequested += OnOpenRequestedAsync;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await StartAsync();
        }
    }

    /// <summary>
    /// The first-render start: reads the viewer's zone and Clear watermark (interop is open now), subscribes for the user's
    /// run changes and loads. Nothing for an anonymous viewer, and only once.
    /// </summary>
    internal async Task StartAsync()
    {
        if (started || disposed || IsAnonymous)
        {
            return;
        }

        started = true;
        timeZone = await ReadTimeZoneAsync();
        clearedAtUtc = await ReadClearedAsync();

        if (disposed)
        {
            return;
        }

        subscription = Notifier.Subscribe(User.Id, OnRunChangedAsync);
        await ReloadAsync();
    }

    /// <summary>Reads the activity again; a run that finished since the last read raises one snackbar.</summary>
    internal async Task ReloadAsync()
    {
        if (disposed || IsAnonymous)
        {
            return;
        }

        ErrorOr<IReadOnlyList<ActivityItem>> result;

        try
        {
            result = await Dispatcher.SendAsync(new GetMyActivityQuery(Clock.UtcNow - window));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogLoadThrew(Logger, exception);
            Fail();
            return;
        }

        if (disposed)
        {
            return;
        }

        if (result.IsError)
        {
            LogLoadRefused(Logger, result.FirstError.Code, result.FirstError.Description);
            Fail();
            return;
        }

        IReadOnlyList<ActivityRow> next = ActivityRows.Build(result.Value, Catalog, Views, ActivityRows.RecordsText(Localizer, Formatter));

        if (loaded)
        {
            AnnounceFinished(rows, next);
        }

        rows = next;
        loaded = true;
        UpdatePolling();
        StateHasChanged();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Set first: a reload still awaiting its read must not start a timer nobody will dispose.
        disposed = true;
        Panel.OpenRequested -= OnOpenRequestedAsync;
        subscription?.Dispose();
        subscription = null;
        StopPolling();
    }

    // A failed load hides the indicator rather than showing an error in the app bar; it was logged.
    private void Fail()
    {
        loaded = false;
        open = false;
        rows = [];
        StopPolling();
        StateHasChanged();
    }

    // Called on the publisher's thread. A publish racing the circuit's disposal can still arrive once afterwards.
    private async Task OnRunChangedAsync(RunChanged change)
    {
        if (disposed)
        {
            return;
        }

        try
        {
            await InvokeAsync(ReloadAsync);
        }
        catch (ObjectDisposedException)
        {
            // The circuit's renderer went away between the check and the call: nobody is left to show the change.
        }
    }

    private Task OnOpenRequestedAsync() =>
        disposed
            ? Task.CompletedTask
            : InvokeAsync(async () =>
            {
                open = true;
                await ReloadAsync();
                StateHasChanged();
            });

    private async Task ToggleAsync()
    {
        open = !open;

        if (open)
        {
            await ReloadAsync();
        }
    }

    private void Close() => open = false;

    private void AnnounceFinished(IReadOnlyList<ActivityRow> previous, IReadOnlyList<ActivityRow> next)
    {
        HashSet<BackgroundRunRef> wasRunning = [.. previous.Where(row => row.IsRunning).Select(row => row.Ref)];

        foreach (ActivityRow row in next.Where(row => !row.IsRunning && wasRunning.Contains(row.Ref)))
        {
            ShowFinished(row);
        }
    }

    private void ShowFinished(ActivityRow row)
    {
        string command = CommandText(row);
        (string message, Severity severity) = row.State switch
        {
            ActivityState.Succeeded => (
                Localizer.Text(ScheduledTaskUiTexts.Activity.FinishedSucceeded, new { Command = command, row.Target }), Severity.Success),
            ActivityState.Failed => (
                Localizer.Text(ScheduledTaskUiTexts.Activity.FinishedFailed, new { Command = command, row.Target }), Severity.Error),
            _ => (
                Localizer.Text(
                    ScheduledTaskUiTexts.Activity.FinishedPartly,
                    new { Command = command, Done = Formatter.Number(row.Done), Total = Formatter.Number(row.Total) }),
                Severity.Warning),
        };

        Snackbar.Add(message, severity, options =>
        {
            if (row.Href is { } href)
            {
                options.Action = Localizer.Text(CommonTexts.Open);
                options.OnClick = _ =>
                {
                    Navigation.NavigateTo(href);
                    return Task.CompletedTask;
                };
            }
        });
    }

    // The fallback poll: a timer owned by the indicator, on only while a listed run is unfinished, gone with it.
    private void UpdatePolling()
    {
        if (!disposed && rows.Any(row => row.IsRunning))
        {
            if (poller is null)
            {
                poller = new PeriodicTimer(pollInterval);
                _ = PollAsync(poller);
            }
        }
        else
        {
            StopPolling();
        }
    }

    private void StopPolling()
    {
        poller?.Dispose();
        poller = null;
    }

    private async Task PollAsync(PeriodicTimer timer)
    {
        try
        {
            while (await timer.WaitForNextTickAsync())
            {
                // A tick that arrives while the previous read is still going is dropped, so reads never pile up.
                if (disposed || Interlocked.Exchange(ref polling, 1) == 1)
                {
                    continue;
                }

                try
                {
                    await InvokeAsync(ReloadAsync);
                }
                finally
                {
                    Interlocked.Exchange(ref polling, 0);
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A tick has no caller to observe its failure: it is logged and the poll ends; the notifier still reloads.
            LogPollFailed(Logger, exception);
        }
    }

    private async Task<TimeZoneInfo> ReadTimeZoneAsync()
    {
        try
        {
            return await TimeZones.GetTimeZoneAsync();
        }
        catch (InvalidOperationException)
        {
            // No interop in this scope: the app's zone.
            return ScheduledTaskUiFormat.ZoneOf(TimeZoneOptions.Value.TimeZone);
        }
    }

    // The Clear watermark, if this browser stored one. Missing or unreadable hides nothing: the rows simply show again.
    private async Task<DateTime?> ReadClearedAsync()
    {
        string? stored;

        try
        {
            stored = await JS.InvokeAsync<string?>("localStorage.getItem", ClearedKey);
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
        {
            return null;
        }

        return DateTime.TryParse(stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime cleared)
            ? DateTime.SpecifyKind(cleared, DateTimeKind.Utc)
            : null;
    }

    private async Task ClearAsync()
    {
        DateTime? newest = Finished.Max(row => row.CompletedAtUtc);

        if (newest is not { } watermark)
        {
            return;
        }

        clearedAtUtc = watermark;

        try
        {
            await JS.InvokeVoidAsync("localStorage.setItem", ClearedKey, watermark.ToString("O", CultureInfo.InvariantCulture));
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
        {
            // Not stored: the rows stay hidden for this circuit and show again after a reload, which is harmless.
        }
    }

    private string CommandText(ActivityRow row) => row.CommandLabel is { } label ? Localizer.Text(label) : row.CommandName;

    private string WhenText(ActivityRow row) =>
        row.IsRunning
            ? Elapsed(Clock.UtcNow - row.StartedAtUtc)
            : ScheduledTaskUiFormat.Time(Formatter, timeZone, row.CompletedAtUtc ?? row.StartedAtUtc);

    // Minutes and seconds, digits in the culture's own (a duration, not a date, so no calendar).
    private string Elapsed(TimeSpan elapsed)
    {
        long seconds = Math.Max(0, (long)elapsed.TotalSeconds);
        long second = seconds % 60;

        return $"{Formatter.Number(seconds / 60)}:{Formatter.Number(second / 10)}{Formatter.Number(second % 10)}";
    }

    private string ProgressText(ActivityRow row) => $"{Formatter.Number(row.Done)} / {Formatter.Number(row.Total)}";

    // The first error of a one-record failure, as the platform renders an error code: its declared detail.
    private string? ErrorText(ActivityRow row) =>
        row is { State: ActivityState.Failed, Total: 1, FirstErrorCode: { } code }
            ? Localizer.Text(ErrorTexts.For(code, ErrorType.Failure).Detail)
            : null;

    private static string IconOf(ActivityRow row) =>
        row.State switch
        {
            ActivityState.Running => Icons.Material.Outlined.Pending,
            ActivityState.Succeeded => Icons.Material.Outlined.CheckCircle,
            ActivityState.PartlyFailed => Icons.Material.Outlined.Warning,
            _ => Icons.Material.Outlined.Error,
        };

    private static Color ColorOf(ActivityRow row) =>
        row.State switch
        {
            ActivityState.Running => Color.Primary,
            ActivityState.Succeeded => Color.Success,
            ActivityState.PartlyFailed => Color.Warning,
            _ => Color.Error,
        };

    [LoggerMessage(Level = LogLevel.Warning, Message = "The activity indicator could not load the user's runs ({Code}: {Description}); it is hidden.")]
    private static partial void LogLoadRefused(ILogger logger, string code, string description);

    [LoggerMessage(Level = LogLevel.Error, Message = "The activity indicator failed to load the user's runs; it is hidden.")]
    private static partial void LogLoadThrew(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "The activity indicator's poll failed; it stopped polling.")]
    private static partial void LogPollFailed(ILogger logger, Exception exception);
}
