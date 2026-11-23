using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MudBlazor;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Application.Tasks;
using Sergin.SharedKernel.Application.Tasks.Commands;
using Sergin.SharedKernel.Application.Tasks.Queries;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Domain.Tasks;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Scopes;
using Sergin.SharedKernel.Presentation.Errors;
using Sergin.SharedKernel.Presentation.Localizations;
using Sergin.SharedKernel.Presentation.Blazor.TimeZones;
using Sergin.SharedKernel.Presentation.Blazor.Dialogs;
using Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

namespace Sergin.SharedKernel.Presentation.Blazor.Tasks;

/// <summary>
/// The schedules that target one record, as an embedded, outlined section of the record's detail view (or, with no
/// <see cref="TargetId"/>, the schedules of one collection task in one scope, as <see cref="SerginTaskSchedulesDialog"/> shows them): command,
/// schedule, next run, last outcome and state, with Run now, Pause or Resume, Edit and Delete on each row, a click on a
/// row opening its runs, and below the table the latest one-off runs over the record (or of the collection task in the scope), a Running one with Cancel. It shows nothing at all to a user
/// who may not manage scheduled tasks.
/// </summary>
/// <remarks>
/// It has no route and holds no module knowledge, only the module's <see cref="Schema"/> and the record's id. Edit
/// reopens the Schedule dialog on the saved schedule: a command a generated view shows has its arguments there as saved,
/// editable and validated as a start's are; a command no view names keeps its arguments as saved (the section rebuilds the
/// command from the catalogue) and changes only its time and zone.
/// A command is shown by its view label, the one the activity indicator uses, or by its stable name when no view names it.
/// </remarks>
public sealed partial class SerginTaskSchedulesSection : IDisposable
{
    /// <summary>The id of the section's root element, so a view can scroll to it or link to it.</summary>
    public const string ElementId = "sergin-task-schedules";

    private const int RecentTake = 5;

    private IReadOnlyCollection<TaskScheduleListItem> tasks = [];
    private IReadOnlyCollection<TaskRunListItem> recentRuns = [];
    private IReadOnlyDictionary<Guid, string> names = new Dictionary<Guid, string>();
    private TimeZoneInfo timeZone = TimeZoneInfo.Utc;
    private SerginProblem? problem;
    private bool visible;
    private Timer? poller;
    private int polling;
    private volatile bool disposed;
    private (string Schema, Guid? TargetId, string? TaskName, Guid? ScopeId)? loadedFor;

    /// <summary>The owning module's schema.</summary>
    [Parameter]
    [EditorRequired]
    public string Schema { get; set; } = string.Empty;

    /// <summary>
    /// The record whose schedules are listed; null to list a collection task's, named by <see cref="TaskName"/> and
    /// <see cref="ScopeId"/>.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public Guid? TargetId { get; set; }

    /// <summary>
    /// <see cref="TargetId"/>'s record as a person knows it (the detail view's title). A schedule stores its targets' ids
    /// only, so Edit names this record by it and any other target by its id.
    /// </summary>
    [Parameter]
    public string? TargetLabel { get; set; }

    /// <summary>With no <see cref="TargetId"/>, the collection task whose schedules and one-off runs are listed.</summary>
    [Parameter]
    public string? TaskName { get; set; }

    /// <summary>With no <see cref="TargetId"/>, the scope the collection task's schedules and runs are listed in.</summary>
    [Parameter]
    public Guid? ScopeId { get; set; }

    /// <summary>When set, only the schedules of that command show; the count of each command still reads the full load.</summary>
    [Parameter]
    public string? CommandName { get; set; }

    /// <summary>
    /// The filtered command's label, which the filter chip names it by; without one the chip shows <see cref="CommandName"/>,
    /// the schedule name, as stored.
    /// </summary>
    [Parameter]
    public TextDefinition? CommandLabel { get; set; }

    /// <summary>Raised with <see langword="null"/> when the filter chip is closed.</summary>
    [Parameter]
    public EventCallback<string?> CommandNameChanged { get; set; }

    /// <summary>Raised after every successful load, so a host can read <see cref="CountOf"/>.</summary>
    [Parameter]
    public EventCallback Loaded { get; set; }

    /// <summary>
    /// How often the section reads its runs again while one it lists is still Running; two seconds by default. It is a
    /// parameter so a test can shorten it.
    /// </summary>
    [Parameter]
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    [Inject]
    private ILogger<SerginTaskSchedulesSection> Logger { get; set; } = default!;

    [Inject]
    private IScopeAuthorization ScopeAuthorization { get; set; } = default!;

    [Inject]
    private ISerginDispatcher Dispatcher { get; set; } = default!;

    [Inject]
    private IUiErrorPresenter ErrorPresenter { get; set; } = default!;

    [Inject]
    private IDialogService Dialogs { get; set; } = default!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    [Inject]
    private ICultureFormatter Formatter { get; set; } = default!;

    [Inject]
    private IUiTimeZoneStore TimeZones { get; set; } = default!;

    [Inject]
    private IOptions<SerginTimeZoneOptions> TimeZoneOptions { get; set; } = default!;

    [Inject]
    private TaskScheduleNextRuns NextRuns { get; set; } = default!;

    [Inject]
    private IDateTimeProvider Clock { get; set; } = default!;

    [Inject]
    private IUserDirectory Users { get; set; } = default!;

    [Inject]
    private TaskCatalog Catalog { get; set; } = default!;

    [Inject]
    private AggregateViewRegistry Views { get; set; } = default!;

    [Inject]
    private SerginScopeSession Scopes { get; set; } = default!;

    // Read for its FiresTriggers only: a schedule saved with an engine that fires nothing must not look as if it will run.
    [Inject]
    private ITaskEngine Engine { get; set; } = default!;

    /// <summary>The schedules (not one-off runs) of a command in the last load; 0 before the first.</summary>
    public int CountOf(string commandName) => tasks.Count(task => task.CommandName == commandName);

    private IReadOnlyCollection<TaskScheduleListItem> shownTasks =>
        CommandName is null ? tasks : [.. tasks.Where(task => task.CommandName == CommandName)];

    private string? FilteredCommandText => CommandLabel is { } label ? Localizer.Text(label) : CommandName;

    private TextDefinition EmptyText => TargetId is null ? TaskUiTexts.EmptyInScope : TaskUiTexts.Empty;

    private Task ClearFilterAsync() => CommandNameChanged.InvokeAsync(null);

    /// <summary>Reads the schedules and the one-off runs again — after the host saved a schedule.</summary>
    public async Task ReloadAsync()
    {
        bool loaded = await LoadAsync();
        // A read that failed stops the timer: its error is shown once, not again on every tick.
        UpdatePolling(keep: loaded);

        if (loaded)
        {
            await Loaded.InvokeAsync();
        }

        // The host calls this from its own handler, so this component is not re-rendered by anything else (the parent skips
        // a child whose parameters did not change).
        await InvokeAsync(StateHasChanged);
    }

    private async Task<bool> LoadAsync()
    {
        if (!visible)
        {
            return true;
        }

        ErrorOr<IReadOnlyCollection<TaskScheduleListItem>> loaded = TargetId is { } targetId
            ? await Dispatcher.SendAsync(new GetTaskSchedulesForTargetQuery(Schema, targetId))
            : await Dispatcher.SendAsync(new GetTaskSchedulesForCollectionQuery(Schema, CollectionTaskName, CollectionScopeId));

        if (loaded.IsError)
        {
            problem = ErrorPresenter.Present(loaded.FirstError);
            return false;
        }

        problem = null;
        tasks = loaded.Value;

        ErrorOr<IReadOnlyCollection<TaskRunListItem>> recent = TargetId is { } target
            ? await Dispatcher.SendAsync(new GetRecentRunsForTargetQuery(Schema, target, RecentTake))
            : await Dispatcher.SendAsync(new GetRecentRunsForCollectionQuery(Schema, CollectionTaskName, CollectionScopeId, RecentTake));

        if (recent.IsError)
        {
            ErrorPresenter.Notify(recent.Errors);
            return false;
        }

        recentRuns = recent.Value;
        names = await Users.GetDisplayNamesAsync([.. recentRuns.Select(run => run.ActingUserId).Distinct()]);

        return true;
    }

    private string CollectionTaskName => TaskName ?? throw new InvalidOperationException(
        "A task schedules section with no TargetId lists a collection task's schedules: set TaskName and ScopeId.");

    private Guid CollectionScopeId => ScopeId ?? throw new InvalidOperationException(
        "A task schedules section with no TargetId lists a collection task's schedules: set TaskName and ScopeId.");

    protected override async Task OnInitializedAsync()
    {
        visible = await ScopeAuthorization.CurrentUserHasAsync(TaskPermissions.Manage);

        if (!visible)
        {
            return;
        }

        // Interop is closed while prerendering: the app's zone until the first render, then the viewer's own.
        timeZone = TaskUiFormat.ZoneOf(TimeZoneOptions.Value.TimeZone);
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!visible || loadedFor == (Schema, TargetId, TaskName, ScopeId))
        {
            return;
        }

        // A new record (or the first one): the previous record's lists and timer go, and this record's are read.
        loadedFor = (Schema, TargetId, TaskName, ScopeId);
        StopPolling();
        tasks = [];
        recentRuns = [];
        names = new Dictionary<Guid, string>();
        await ReloadAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !visible)
        {
            return;
        }

        TimeZoneInfo browser = await TimeZones.GetTimeZoneAsync();

        if (browser.Id != timeZone.Id)
        {
            timeZone = browser;
            StateHasChanged();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Set first: a reload still awaiting its read must not start a timer nobody will dispose.
        disposed = true;
        poller?.Dispose();
        poller = null;
    }

    /// <summary>Whether the section's poll timer is running (it is while a listed run is Running, never after disposal).</summary>
    public bool IsPolling => poller is not null;

    private bool AnyRunning => recentRuns.Any(run => run.Status == TaskRunStatus.Running)
        || tasks.Any(task => task.LastRunStatus == TaskRunStatus.Running);

    // A timer owned by the section: on only while a listed run is Running, off again once none is, and gone with the section.
    private void UpdatePolling(bool keep = true)
    {
        if (keep && AnyRunning && !disposed)
        {
            poller ??= new Timer(tick => InvokeAsync(PollAsync), null, PollInterval, PollInterval);
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

    private async Task PollAsync()
    {
        // A tick that arrives while the previous read is still going is dropped, so reads never pile up.
        if (disposed || poller is null || Interlocked.Exchange(ref polling, 1) == 1)
        {
            return;
        }

        try
        {
            await ReloadAsync();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A tick has no caller to observe its failure: it is logged, and the timer stops rather than fail again every tick.
            LogPollFailed(Logger, exception, Schema, TargetId, TaskName);
            StopPolling();
        }
        finally
        {
            Interlocked.Exchange(ref polling, 0);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The scheduled tasks section of {Schema} record {TargetId} (task {TaskName}) failed to read its runs; it stopped polling.")]
    private static partial void LogPollFailed(ILogger logger, Exception exception, string schema, Guid? targetId, string? taskName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Cancelling scheduled run {RunId} of {Schema} failed.")]
    private static partial void LogCancelFailed(ILogger logger, Exception exception, string schema, Guid runId);

    private string NextRunText(TaskScheduleListItem task)
    {
        if (task.State != TaskScheduleState.Active)
        {
            return Localizer.Text(TaskUiTexts.NotScheduled);
        }

        var now = new DateTimeOffset(DateTime.SpecifyKind(Clock.UtcNow, DateTimeKind.Utc));

        return NextRuns.Next(task.Cron, task.TimeZoneId, now, 1) is [var next]
            ? Formatter.DateTime(next, timeZone)
            : Localizer.Text(TaskUiTexts.NextRunUnavailable);
    }

    private string LastOutcomeText(TaskScheduleListItem task)
    {
        if (task.LastRunStatus is not { } status)
        {
            return Localizer.Text(TaskUiTexts.Never);
        }

        var arguments = new
        {
            Status = Localizer.Text(TaskUiFormat.StatusText(status)),
            When = task.LastRunAtUtc is { } at ? Time(at) : string.Empty,
            Failed = Formatter.Number(task.LastRunFailed ?? 0),
        };

        return Localizer.Text(
            task.LastRunFailed is > 0 ? TaskUiTexts.Runs.LastOutcomeWithFailures : TaskUiTexts.Runs.LastOutcome,
            arguments);
    }

    // "Manual" for a run a person started, "One-off" for a Run once: the table holds both.
    private string TriggerText(TaskRunTrigger trigger) => Localizer.Text(TaskUiFormat.TriggerText(trigger));

    private string Time(DateTime utc) => TaskUiFormat.Time(Formatter, timeZone, utc);

    private string NameOf(Guid userId) => TaskUiFormat.NameOf(names, userId);

    private string RunSummaryText(TaskRunListItem run) => TaskUiFormat.RunSummary(Localizer, Formatter, run);

    // A schedule and a one-off run are named by their command's view label (Read profile), as the activity indicator
    // names it; a command no view names shows its stable name, as stored.
    private string CommandText(string commandName) =>
        ActivityRows.CommandLabel(commandName, Catalog, Views) is { } label ? Localizer.Text(label) : commandName;

    // The icon its view command shows by, beside the name; none when no view names the command or it declares no icon.
    private string? CommandIcon(string commandName) => ActivityRows.CommandIcon(commandName, Catalog, Views);

    private async Task ShowRunsAsync(TableRowClickEventArgs<TaskScheduleListItem> click)
    {
        DialogParameters<SerginTaskRunsDialog> parameters = new()
        {
            { dialog => dialog.Schema, Schema },
            { dialog => dialog.ScheduleId, new TaskScheduleId(click.Item!.Id) },
            { dialog => dialog.CommandName, click.Item.CommandName },
        };

        await Dialogs.ShowAsync<SerginTaskRunsDialog>(
            string.Empty, parameters, new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true, CloseOnEscapeKey = true });
    }

    /// <summary>
    /// Cancels a Running one-off run, then reads the lists again whatever the answer. A run that finished meanwhile is no
    /// error to the user (the reload shows how it ended); any other refusal is notified, and a send that throws says the
    /// run could not be cancelled rather than faulting the circuit.
    /// </summary>
    internal async Task CancelRunAsync(Guid runId)
    {
        try
        {
            ErrorOr<Success> result = await Dispatcher.SendAsync(new CancelTaskRunCommand(Schema, runId));

            if (result.IsError && result.FirstError.Code != TaskErrors.RunFinished.Code)
            {
                ErrorPresenter.Notify(result.FirstError);
            }
        }
        catch (Exception exception)
        {
            // A click handler: anything escaping here would fault the circuit.
            LogCancelFailed(Logger, exception, Schema, runId);
            Snackbar.Add(Localizer.Text(TaskUiTexts.Activity.CancelFailed), Severity.Error);
        }

        await ReloadAsync();
    }

    private async Task RunNowAsync(TaskScheduleListItem task)
    {
        ErrorOr<TaskScheduleRunStartedResponse> result =
            await Dispatcher.SendAsync(new RunTaskScheduleNowCommand(Schema, new TaskScheduleId(task.Id)));

        if (result.IsError)
        {
            ErrorPresenter.Notify(result.Errors);
        }
        else
        {
            Snackbar.Add(Localizer.Text(TaskUiTexts.RunOnceStarted), Severity.Success);
        }

        await ReloadAsync();
    }

    private async Task TogglePauseAsync(TaskScheduleListItem task)
    {
        var id = new TaskScheduleId(task.Id);
        bool pausing = task.State == TaskScheduleState.Active;
        IRequest<ErrorOr<Success>> command = pausing ? new PauseTaskScheduleCommand(Schema, id) : new ResumeTaskScheduleCommand(Schema, id);

        if (await SendAtCurrentVersionAsync(id, command))
        {
            Snackbar.Add(Localizer.Text(pausing ? TaskUiTexts.WasPaused : TaskUiTexts.WasResumed), Severity.Success);
        }

        await ReloadAsync();
    }

    /// <summary>Asks, then deletes the schedule. The confirm names the task as the section's rows do. Internal for the tests.</summary>
    internal async Task DeleteAsync(TaskScheduleListItem task)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (!await Dialogs.ConfirmDeleteAsync(
                Localizer, TaskUiTexts.DeleteTitle, TaskUiTexts.DeleteConfirm, new { Command = CommandText(task.CommandName) }))
        {
            return;
        }

        var id = new TaskScheduleId(task.Id);

        if (await SendAtCurrentVersionAsync(id, new DeleteTaskScheduleCommand(Schema, id)))
        {
            Snackbar.Add(Localizer.Text(TaskUiTexts.WasDeleted), Severity.Success);
        }

        await ReloadAsync();
    }

    private async Task EditAsync(TaskScheduleListItem item)
    {
        // Whatever the dialog did (saved, ran once, cancelled), the lists are read again.
        if (await EditRequestAsync(new TaskScheduleId(item.Id)) is { } request)
        {
            await Dialogs.ShowScheduleAsync(request);
        }

        await ReloadAsync();
    }

    /// <summary>
    /// The Schedule dialog's request for changing a saved schedule, or null (with the error shown) when it cannot be read or
    /// its command is no longer known. A command a generated view shows opens with its arguments as saved, editable; one no
    /// view names keeps them as they are. Internal for the schedule dialog tests.
    /// </summary>
    internal async Task<SerginScheduleRequest?> EditRequestAsync(TaskScheduleId id)
    {
        ErrorOr<Versioned<TaskScheduleResponse>> loaded = await Dispatcher.SendVersionedAsync(new GetTaskScheduleByIdQuery(Schema, id));

        if (loaded.IsError)
        {
            ErrorPresenter.Notify(loaded.Errors);
            return null;
        }

        TaskScheduleResponse task = loaded.Value.Value;

        // A collection task's schedule has no targets; any other names at least one.
        if (Catalog.ByName(task.CommandName) is not { } command
            || command.TargetKind != CommandTargetKind.Collection && task.Targets.Count == 0)
        {
            ErrorPresenter.Notify(TaskErrors.UnknownCommand);
            return null;
        }

        bool collection = command.TargetKind == CommandTargetKind.Collection;
        Guid? targetId = collection ? null : task.Targets.First().TargetId;
        ErrorOr<IBaseCommand> template = SerginScheduleArguments.Rebuild(command, task);

        if (template.IsError)
        {
            ErrorPresenter.Notify(template.Errors);
            return null;
        }

        IReadOnlyList<string> labels = collection && ScopeId is { } scope
            ? [TaskUiFormat.ScopeName(Scopes, scope)]
            : [.. task.Targets.Select(target => target.TargetId == TargetId && TargetLabel is { } label ? label : target.TargetId.ToString())];
        TextDefinition? caption = collection ? TaskUiTexts.Dialog.Scope : null;

        if (ActivityRows.CommandViewOf(task.CommandName, Catalog, Views) is { Task: not null } view && view.TaskFields.Count > 0)
        {
            // The fields start from the stored arguments (the dialog reads them again with the version, and after a stale
            // save), and the template is rebuilt from what the person leaves there. A NotShown argument has no field: the
            // template gives it its declared default.
            GeneratedFormValues arguments = SerginScheduleArguments.Of(view, template.Value);

            return new SerginScheduleRequest(
                Schema,
                TaskUiTexts.EditTitle,
                () => GeneratedViewSupport.TaskTemplateOf(view, targetId, arguments.Values),
                task.Targets,
                labels,
                EditingId: id,
                TargetsCaption: caption,
                TaskView: view,
                ArgumentValues: arguments);
        }

        // The catalogue holds tasks only, so the command it rebuilt is one.
        var saved = (ITaskCommand)template.Value;

        return new SerginScheduleRequest(Schema, TaskUiTexts.EditTitle, () => saved, task.Targets, labels, EditingId: id, TargetsCaption: caption);
    }

    // The version is read just before the write, as a detail page's delete does; a stale answer shows as its own message.
    private async Task<bool> SendAtCurrentVersionAsync(TaskScheduleId id, IRequest<ErrorOr<Success>> command)
    {
        ErrorOr<Versioned<TaskScheduleResponse>> loaded = await Dispatcher.SendVersionedAsync(new GetTaskScheduleByIdQuery(Schema, id));

        if (loaded.IsError)
        {
            ErrorPresenter.Notify(loaded.Errors);
            return false;
        }

        ErrorOr<Versioned<Success>> result = await Dispatcher.SendVersionedAsync(command, loaded.Value.Version);

        if (result.IsError)
        {
            ErrorPresenter.Notify(result.Errors);
            return false;
        }

        return true;
    }
}
