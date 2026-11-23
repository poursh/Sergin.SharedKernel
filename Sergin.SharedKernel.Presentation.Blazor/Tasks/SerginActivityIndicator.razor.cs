using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using MudBlazor;
using Sergin.SharedKernel.Application.Tasks;
using Sergin.SharedKernel.Application.Tasks.Activity;
using Sergin.SharedKernel.Application.Tasks.Commands;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Scopes;
using Sergin.SharedKernel.Presentation.Blazor.TimeZones;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Tasks;

/// <summary>
/// The app bar's activity indicator (an Activity button opening a modal): the signed-in user's own background runs from every module, running or finished in
/// the last 24 hours. It loads after the first interactive render, then reloads whenever <see cref="IRunNotifier"/> says
/// one of the user's runs changed (at most once a second: a change inside that second schedules one trailing reload), and
/// every 5 seconds while a run is unfinished (a run executed by another process publishes nothing here). A running row can
/// be cancelled from here. Every applied load is handed to <see cref="ActivityPanelState"/>, and a finished run is
/// announced once per circuit, however often it leaves the read and comes back. It never breaks the shell: an anonymous
/// viewer and a failed load render nothing.
/// </summary>
public sealed partial class SerginActivityIndicator : IDisposable
{
    private const string ClearedKeyPrefix = "sergin.activity.cleared.";

    private static readonly TimeSpan window = TimeSpan.FromHours(24);
    private static readonly TimeSpan pollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan notifiedReloadInterval = TimeSpan.FromSeconds(1);

    // The panel is a modal: Escape and a backdrop click close it, as its Close button does.
    private static readonly DialogOptions dialogOptions = new()
    {
        MaxWidth = MaxWidth.Small,
        FullWidth = true,
        CloseOnEscapeKey = true,
        BackdropClick = true,
    };

    private readonly HashSet<TaskRunRef> announced = [];
    private IReadOnlyList<ActivityRow> rows = [];
    private Func<string, TextDefinition?>? moduleCaption;
    private TimeZoneInfo timeZone = TimeZoneInfo.Utc;
    private DateTime? clearedAtUtc;
    private IDisposable? subscription;
    private PeriodicTimer? poller;
    private bool loaded;
    private bool started;
    private bool open;
    private volatile bool disposed;
    private bool reloading;
    private bool reloadPending;
    private long lastNotifiedReload;
    private bool notifiedReloadScheduled;
    private DateTime? announceStartedSinceUtc;

    [Inject]
    private ISerginDispatcher Dispatcher { get; set; } = default!;

    [Inject]
    private IRunNotifier Notifier { get; set; } = default!;

    [Inject]
    private IUserContext User { get; set; } = default!;

    [Inject]
    private TaskCatalog Catalog { get; set; } = default!;

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
    private SerginUiModuleCatalog Modules { get; set; } = default!;

    [Inject]
    private SerginScopeSession Scopes { get; set; } = default!;

    [Inject]
    private IUiErrorPresenter ErrorPresenter { get; set; } = default!;

    [Inject]
    private ILogger<SerginActivityIndicator> Logger { get; set; } = default!;

    /// <summary>Whether the indicator hears the user's run changes (from the first load until disposal).</summary>
    internal bool IsSubscribed => subscription is not null;

    /// <summary>Whether the fallback poll runs (while a listed run is unfinished, never after disposal).</summary>
    internal bool IsPolling => poller is not null;

    /// <summary>Whether no read is out and no notifier-driven reload is waiting for its interval.</summary>
    internal bool IsIdle => !reloading && !notifiedReloadScheduled;

    // The shape every anonymous context has: ClaimsPrincipalUserContext.Anonymous and the API's stub alike carry no id.
    private bool IsAnonymous => User.Id.Value == Guid.Empty;

    private bool Visible => loaded && !IsAnonymous;

    private int RunningCount => rows.Count(row => row.IsRunning);

    private string RunningCountText => Formatter.Number(RunningCount);

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
                ? [(TaskUiTexts.Activity.Running, Running)]
                : [(TaskUiTexts.Activity.Running, Running), (TaskUiTexts.Activity.Finished, finished)];
        }
    }

    private string OpenLabel => Localizer.Text(TaskUiTexts.Activity.OpenLabel, new { Count = Formatter.Number(RunningCount) });

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

        try
        {
            timeZone = await ReadTimeZoneAsync();
            clearedAtUtc = await ReadClearedAsync();

            if (disposed)
            {
                return;
            }

            subscription = Notifier.Subscribe(User.Id, OnRunChangedAsync);
            await ReloadAsync();
        }
        catch (Exception exception)
        {
            // Called from OnAfterRenderAsync: anything escaping here would fault the circuit, so it hides the indicator.
            LogStartFailed(Logger, exception);

            if (!disposed)
            {
                Fail();
            }
        }
    }

    /// <summary>
    /// Reads the activity again; a run that finished since the last read raises one snackbar. Single-flight: a reload asked
    /// for while one is reading (a burst of notifier events as a bulk run's items finish, a poll tick) is not started
    /// beside it but runs once more after it, however many were asked for. So reads are applied in the order they were
    /// made, an older read never overwrites a newer one, and a burst costs at most two reads. Every caller is on the
    /// circuit's synchronization context (InvokeAsync), so the two flags need no lock.
    /// </summary>
    internal async Task ReloadAsync()
    {
        if (disposed || IsAnonymous)
        {
            return;
        }

        if (reloading)
        {
            reloadPending = true;
            return;
        }

        reloading = true;

        try
        {
            do
            {
                reloadPending = false;
                await LoadOnceAsync();
            }
            while (reloadPending && !disposed);
        }
        finally
        {
            reloading = false;
        }
    }

    private async Task LoadOnceAsync()
    {
        ErrorOr<IReadOnlyList<ActivityItem>> result;
        DateTime readAtUtc = Clock.UtcNow;

        try
        {
            result = await Dispatcher.SendAsync(new GetMyActivityQuery(readAtUtc - window));
        }
        catch (Exception exception)
        {
            // No token is in play, so a cancellation is a failure like any other (an interop or circuit timeout).
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

        moduleCaption ??= ActivityRows.ModuleCaption(Modules);
        IReadOnlyList<ActivityRow> next =
            ActivityRows.Build(
                result.Value,
                Catalog,
                Views,
                ActivityRows.RecordsText(Localizer, Formatter),
                moduleCaption,
                scopeId => Scopes.Options.FirstOrDefault(option => option.Id == scopeId)?.Name);

        if (loaded)
        {
            AnnounceFinished(rows, next);
        }
        else
        {
            // The first successful load (or the first after a failure) announces nothing; a run started after it is new.
            announceStartedSinceUtc = readAtUtc;
        }

        rows = next;
        loaded = true;
        Panel.SetRows(rows);
        UpdatePolling();
        StateHasChanged();
    }

    /// <summary>
    /// Cancels one of the listed runs, then reloads whatever the answer. A run that finished meanwhile is no error to the
    /// user (the reload shows how it ended); any other refusal is notified, and a send that throws says the run could not
    /// be cancelled rather than faulting the circuit.
    /// </summary>
    internal async Task CancelAsync(TaskRunRef run)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (disposed)
        {
            return;
        }

        try
        {
            ErrorOr<Success> result = await Dispatcher.SendAsync(new CancelTaskRunCommand(run.Schema, run.RunId));

            if (result.IsError && result.FirstError.Code != TaskErrors.RunFinished.Code)
            {
                ErrorPresenter.Notify(result.FirstError);
            }
        }
        catch (Exception exception)
        {
            // A click handler: anything escaping here would fault the circuit.
            LogCancelFailed(Logger, exception);
            Snackbar.Add(Localizer.Text(TaskUiTexts.Activity.CancelFailed), Severity.Error);
        }

        await ReloadAsync();
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
        Panel.SetRows(rows);
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
            await InvokeAsync(ReloadOnChangeAsync);
        }
        catch (ObjectDisposedException)
        {
            // The circuit's renderer went away between the check and the call: nobody is left to show the change.
        }
    }

    // On the circuit's context. At most one notifier-driven reload a second: a change inside the interval schedules one
    // trailing reload (and a change while it waits is covered by it), so nothing is lost and a burst costs one more read.
    private Task ReloadOnChangeAsync()
    {
        if (disposed || notifiedReloadScheduled)
        {
            return Task.CompletedTask;
        }

        TimeSpan since = lastNotifiedReload == 0 ? TimeSpan.MaxValue : Stopwatch.GetElapsedTime(lastNotifiedReload);

        if (since >= notifiedReloadInterval)
        {
            lastNotifiedReload = Stopwatch.GetTimestamp();
            return ReloadAsync();
        }

        notifiedReloadScheduled = true;
        _ = TrailingReloadAsync(notifiedReloadInterval - since);
        return Task.CompletedTask;
    }

    // The wait is at most a second and holds nothing, so it is not cancelled: disposal is checked once it ends.
    private async Task TrailingReloadAsync(TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay);

            if (disposed)
            {
                return;
            }

            await InvokeAsync(async () =>
            {
                notifiedReloadScheduled = false;

                if (!disposed)
                {
                    lastNotifiedReload = Stopwatch.GetTimestamp();
                    await ReloadAsync();
                }
            });
        }
        catch (ObjectDisposedException)
        {
            // The circuit's renderer went away while waiting: nobody is left to show the change.
        }
        catch (Exception exception)
        {
            // A background reload has no caller to observe its failure; the next change or poll reloads again.
            LogTrailingReloadFailed(Logger, exception);
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

    // A finished row is announced when the previous read had it running, or did not have it at all and it started after
    // the first successful load (a run fast enough to start and finish between two reads); never one already known, and
    // never twice in a circuit: a run whose source dropped out of one read comes back absent from the previous one.
    private void AnnounceFinished(IReadOnlyList<ActivityRow> previous, IReadOnlyList<ActivityRow> next)
    {
        HashSet<TaskRunRef> wasRunning = [.. previous.Where(row => row.IsRunning).Select(row => row.Ref)];
        HashSet<TaskRunRef> known = [.. previous.Select(row => row.Ref)];

        foreach (ActivityRow row in next.Where(row => !row.IsRunning && (wasRunning.Contains(row.Ref) || IsNewSinceFirstLoad(row, known))))
        {
            if (announced.Add(row.Ref))
            {
                ShowFinished(row);
            }
        }
    }

    private bool IsNewSinceFirstLoad(ActivityRow row, HashSet<TaskRunRef> known) =>
        !known.Contains(row.Ref) && announceStartedSinceUtc is { } since && row.StartedAtUtc >= since;

    private void ShowFinished(ActivityRow row)
    {
        string command = CommandText(row);
        (string message, Severity severity) = row.State switch
        {
            RunOutcome.Succeeded => (
                Localizer.Text(TaskUiTexts.Activity.FinishedSucceeded, new { Command = command, row.Target }), Severity.Success),
            RunOutcome.Failed => (
                Localizer.Text(TaskUiTexts.Activity.FinishedFailed, new { Command = command, row.Target }), Severity.Error),
            RunOutcome.Cancelled when row.Done > 0 => (
                Localizer.Text(
                    TaskUiTexts.Activity.FinishedPartlyCancelled,
                    new
                    {
                        Command = command,
                        Done = Formatter.Number(row.Done),
                        Total = Formatter.Number(row.Total),
                        Cancelled = Formatter.Number(row.Cancelled),
                    }),
                Severity.Normal),
            RunOutcome.Cancelled => (
                Localizer.Text(TaskUiTexts.Activity.FinishedCancelled, new { Command = command, row.Target }), Severity.Normal),
            _ => (
                Localizer.Text(
                    TaskUiTexts.Activity.FinishedPartly,
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
            // A tick during a read joins it rather than starting another: ReloadAsync is single-flight.
            while (!disposed && await timer.WaitForNextTickAsync())
            {
                await InvokeAsync(ReloadAsync);
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
        catch (Exception exception) when (exception is InvalidOperationException or JSException or JSDisconnectedException or TaskCanceledException)
        {
            // No interop in this scope, or it failed or timed out: the app's zone.
            return TaskUiFormat.ZoneOf(TimeZoneOptions.Value.TimeZone);
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
            : TaskUiFormat.Time(Formatter, timeZone, row.CompletedAtUtc ?? row.StartedAtUtc);

    // Minutes and seconds, digits in the culture's own (a duration, not a date, so no calendar).
    private string Elapsed(TimeSpan elapsed)
    {
        long seconds = Math.Max(0, (long)elapsed.TotalSeconds);
        long second = seconds % 60;

        return $"{Formatter.Number(seconds / 60)}:{Formatter.Number(second / 10)}{Formatter.Number(second % 10)}";
    }

    private string ProgressText(ActivityRow row) =>
        Localizer.Text(TaskUiTexts.Activity.Progress, new { Done = Formatter.Number(row.Done), Total = Formatter.Number(row.Total) });

    // The first error of a one-record failure as the runs dialog shows it: the item's stored description, else its code.
    private static string? ErrorText(ActivityRow row) =>
        row is { State: RunOutcome.Failed, Total: 1 } ? row.FirstErrorDescription ?? row.FirstErrorCode : null;

    private static string IconOf(ActivityRow row) =>
        row.State switch
        {
            RunOutcome.Running => Icons.Material.Outlined.WorkHistory,
            RunOutcome.Succeeded => Icons.Material.Outlined.CheckCircle,
            RunOutcome.PartlyFailed => Icons.Material.Outlined.Warning,
            RunOutcome.Cancelled => Icons.Material.Outlined.Block,
            _ => Icons.Material.Outlined.Error,
        };

    private static Color ColorOf(ActivityRow row) =>
        row.State switch
        {
            RunOutcome.Running => Color.Primary,
            RunOutcome.Succeeded => Color.Success,
            RunOutcome.PartlyFailed => Color.Warning,
            RunOutcome.Cancelled => Color.Default,
            _ => Color.Error,
        };

    [LoggerMessage(Level = LogLevel.Warning, Message = "The activity indicator could not load the user's runs ({Code}: {Description}); it is hidden.")]
    private static partial void LogLoadRefused(ILogger logger, string code, string description);

    [LoggerMessage(Level = LogLevel.Error, Message = "The activity indicator failed to load the user's runs; it is hidden.")]
    private static partial void LogLoadThrew(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "The activity indicator failed to start; it is hidden.")]
    private static partial void LogStartFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "The activity indicator's reload after a run change failed.")]
    private static partial void LogTrailingReloadFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "The activity indicator could not send a run's cancel.")]
    private static partial void LogCancelFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "The activity indicator's poll failed; it stopped polling.")]
    private static partial void LogPollFailed(ILogger logger, Exception exception);
}
