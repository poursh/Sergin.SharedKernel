using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using MudBlazor;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Application.ScheduledTasks;
using Sergin.SharedKernel.Application.ScheduledTasks.Commands;
using Sergin.SharedKernel.Application.ScheduledTasks.Queries;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Domain.ScheduledTasks;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Pages;
using Sergin.SharedKernel.Presentation.Blazor.Times;
using Sergin.SharedKernel.Presentation.Errors;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.ScheduledTasks;

/// <summary>
/// The schedules that target one record, as an embedded, outlined section of the record's detail view: command,
/// schedule, next run, last outcome and state, with Run now, Pause or Resume, Edit and Delete on each row, a click on a
/// row opening its runs, and below the table the latest one-off runs over the record. It shows nothing at all to a user
/// who may not manage scheduled tasks.
/// </summary>
/// <remarks>
/// It has no route and holds no module knowledge, only the module's <see cref="Schema"/> and the record's id. Edit
/// changes a schedule's time and zone and keeps its arguments as saved: the section rebuilds the saved command from the
/// catalogue, and the module's own arguments form is the module view's to offer through <see cref="SerginScheduleDialog"/>.
/// The command is shown by its stable name, as stored.
/// </remarks>
public sealed partial class SerginScheduledTasksSection
{
    private const int OneOffTake = 5;

    private IReadOnlyCollection<ScheduledTaskListItem> tasks = [];
    private IReadOnlyCollection<ScheduledRunListItem> oneOffRuns = [];
    private IReadOnlyDictionary<Guid, string> names = new Dictionary<Guid, string>();
    private TimeZoneInfo timeZone = TimeZoneInfo.Utc;
    private SerginProblem? problem;
    private bool visible;

    /// <summary>The owning module's schema.</summary>
    [Parameter]
    [EditorRequired]
    public string Schema { get; set; } = string.Empty;

    /// <summary>The record whose schedules are listed.</summary>
    [Parameter]
    [EditorRequired]
    public Guid TargetId { get; set; }

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
    private ScheduledTaskNextRuns NextRuns { get; set; } = default!;

    [Inject]
    private IDateTimeProvider Clock { get; set; } = default!;

    [Inject]
    private IUserDirectory Users { get; set; } = default!;

    [Inject]
    private ScheduledCommandCatalog Catalog { get; set; } = default!;

    // Read for its FiresTriggers only: a schedule saved with an engine that fires nothing must not look as if it will run.
    [Inject]
    private IScheduledTaskEngine Engine { get; set; } = default!;

    /// <summary>Reads the schedules and the one-off runs again — after the host saved a schedule.</summary>
    public async Task ReloadAsync()
    {
        if (!visible)
        {
            return;
        }

        ErrorOr<IReadOnlyCollection<ScheduledTaskListItem>> loaded =
            await Dispatcher.SendAsync(new GetScheduledTasksForTargetQuery(Schema, TargetId));

        if (loaded.IsError)
        {
            problem = ErrorPresenter.Present(loaded.FirstError);
            return;
        }

        problem = null;
        tasks = loaded.Value;

        ErrorOr<IReadOnlyCollection<ScheduledRunListItem>> oneOff =
            await Dispatcher.SendAsync(new GetOneOffRunsForTargetQuery(Schema, TargetId, OneOffTake));

        if (oneOff.IsError)
        {
            ErrorPresenter.Notify(oneOff.Errors);
            return;
        }

        oneOffRuns = oneOff.Value;
        names = await Users.GetDisplayNamesAsync([.. oneOffRuns.Select(run => run.ActingUserId).Distinct()]);
    }

    protected override async Task OnInitializedAsync()
    {
        visible = await ScopeAuthorization.CurrentUserHasAsync(ScheduledTaskPermissions.Manage);

        if (!visible)
        {
            return;
        }

        // Interop is closed while prerendering: the app's zone until the first render, then the viewer's own.
        timeZone = ScheduledTaskUiFormat.ZoneOf(TimeZoneOptions.Value.TimeZone);
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

    private string NextRunText(ScheduledTaskListItem task)
    {
        if (task.State != ScheduledTaskState.Active)
        {
            return Localizer.Text(ScheduledTaskUiTexts.NotScheduled);
        }

        var now = new DateTimeOffset(DateTime.SpecifyKind(Clock.UtcNow, DateTimeKind.Utc));

        return NextRuns.Next(task.Cron, task.TimeZoneId, now, 1) is [var next]
            ? Formatter.DateTime(next, timeZone)
            : Localizer.Text(ScheduledTaskUiTexts.NextRunUnavailable);
    }

    private string LastOutcomeText(ScheduledTaskListItem task)
    {
        if (task.LastRunStatus is not { } status)
        {
            return Localizer.Text(ScheduledTaskUiTexts.Never);
        }

        var arguments = new
        {
            Status = Localizer.Text(ScheduledTaskUiFormat.StatusText(status)),
            When = task.LastRunAtUtc is { } at ? Time(at) : string.Empty,
            Failed = Formatter.Number(task.LastRunFailed ?? 0),
        };

        return Localizer.Text(
            task.LastRunFailed is > 0 ? ScheduledTaskUiTexts.Runs.LastOutcomeWithFailures : ScheduledTaskUiTexts.Runs.LastOutcome,
            arguments);
    }

    private string Time(DateTime utc) => ScheduledTaskUiFormat.Time(Formatter, timeZone, utc);

    private string NameOf(Guid userId) => ScheduledTaskUiFormat.NameOf(names, userId);

    private string OneOffSummaryText(ScheduledRunListItem run) => ScheduledTaskUiFormat.RunSummary(Localizer, Formatter, run);

    private async Task ShowRunsAsync(TableRowClickEventArgs<ScheduledTaskListItem> click)
    {
        DialogParameters<SerginScheduledRunsDialog> parameters = new()
        {
            { dialog => dialog.Schema, Schema },
            { dialog => dialog.TaskId, new ScheduledTaskId(click.Item!.Id) },
            { dialog => dialog.CommandName, click.Item.CommandName },
        };

        await Dialogs.ShowAsync<SerginScheduledRunsDialog>(
            string.Empty, parameters, new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true, CloseOnEscapeKey = true });
    }

    private async Task RunNowAsync(ScheduledTaskListItem task)
    {
        ErrorOr<ScheduledRunStartedResponse> result =
            await Dispatcher.SendAsync(new RunScheduledTaskNowCommand(Schema, new ScheduledTaskId(task.Id)));

        if (result.IsError)
        {
            ErrorPresenter.Notify(result.Errors);
        }
        else
        {
            Snackbar.Add(Localizer.Text(ScheduledTaskUiTexts.RunOnceStarted), Severity.Success);
        }

        await ReloadAsync();
    }

    private async Task TogglePauseAsync(ScheduledTaskListItem task)
    {
        var id = new ScheduledTaskId(task.Id);
        bool pausing = task.State == ScheduledTaskState.Active;
        IRequest<ErrorOr<Success>> command = pausing ? new PauseScheduledTaskCommand(Schema, id) : new ResumeScheduledTaskCommand(Schema, id);

        if (await SendAtCurrentVersionAsync(id, command))
        {
            Snackbar.Add(Localizer.Text(pausing ? ScheduledTaskUiTexts.WasPaused : ScheduledTaskUiTexts.WasResumed), Severity.Success);
        }

        await ReloadAsync();
    }

    private async Task DeleteAsync(ScheduledTaskListItem task)
    {
        if (!await Dialogs.ConfirmDeleteAsync(
                Localizer, ScheduledTaskUiTexts.DeleteTitle, ScheduledTaskUiTexts.DeleteConfirm, new { Command = task.CommandName }))
        {
            return;
        }

        var id = new ScheduledTaskId(task.Id);

        if (await SendAtCurrentVersionAsync(id, new DeleteScheduledTaskCommand(Schema, id)))
        {
            Snackbar.Add(Localizer.Text(ScheduledTaskUiTexts.WasDeleted), Severity.Success);
        }

        await ReloadAsync();
    }

    private async Task EditAsync(ScheduledTaskListItem item)
    {
        var id = new ScheduledTaskId(item.Id);
        ErrorOr<Versioned<ScheduledTaskResponse>> loaded = await Dispatcher.SendVersionedAsync(new GetScheduledTaskByIdQuery(Schema, id));

        if (loaded.IsError)
        {
            ErrorPresenter.Notify(loaded.Errors);
            return;
        }

        ScheduledTaskResponse task = loaded.Value.Value;

        if (!Catalog.TryGet(task.CommandName, out ScheduledCommand? command) || task.Targets.FirstOrDefault() is not { } first)
        {
            ErrorPresenter.Notify(ScheduledTaskErrors.UnknownCommand);
            return;
        }

        // Only the schedule changes here, so the saved arguments are rebuilt as they are; the template's target id is ignored.
        ErrorOr<IBaseCommand> template = ScheduledCommandFactory.Build(command, task.Arguments, first.TargetId, first.RootId);

        if (template.IsError)
        {
            ErrorPresenter.Notify(template.Errors);
            return;
        }

        IBaseCommand saved = template.Value;

        bool changed = await Dialogs.ShowScheduleAsync(new SerginScheduleRequest(
            Schema,
            ScheduledTaskUiTexts.EditTitle,
            () => saved,
            task.Targets,
            [.. task.Targets.Select(target => target.TargetId.ToString())],
            EditingId: id));

        if (changed)
        {
            await ReloadAsync();
        }
    }

    // The version is read just before the write, as a detail page's delete does; a stale answer shows as its own message.
    private async Task<bool> SendAtCurrentVersionAsync(ScheduledTaskId id, IRequest<ErrorOr<Success>> command)
    {
        ErrorOr<Versioned<ScheduledTaskResponse>> loaded = await Dispatcher.SendVersionedAsync(new GetScheduledTaskByIdQuery(Schema, id));

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
