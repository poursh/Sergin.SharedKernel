using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Application.Tasks;
using Sergin.SharedKernel.Application.Tasks.Commands;
using Sergin.SharedKernel.Application.Tasks.Queries;
using Sergin.SharedKernel.Domain.Tasks;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Lists;
using Sergin.SharedKernel.Presentation.Blazor.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Navigation;
using Sergin.SharedKernel.Presentation.Blazor.Scopes;
using Sergin.SharedKernel.Presentation.Blazor.TimeZones;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Tasks.Runs;

/// <summary>
/// Every run of one module's tasks, whoever and whatever started it: the task (a link to the run's records), what it
/// ran on, who started it and how, when, and where it stands, with Cancel on a running row. The modules are the schemas
/// the task catalogue names; with none the view says so and sends nothing (no handler would answer). With more than one,
/// a selector picks the module, kept in the query string as <c>?module=</c>; an unknown value falls back to the first in
/// drawer order. The list reloads every 5 seconds while a row it shows is running: the run notifier publishes per acting
/// user, and this page lists everyone's runs.
/// </summary>
public sealed partial class SerginTaskRunsView : IAsyncDisposable
{
    private const string ModuleParameter = "module";

    private static readonly TimeSpan pollInterval = TimeSpan.FromSeconds(5);

    private static readonly TaskRunTrigger[] triggers =
        [TaskRunTrigger.Manual, TaskRunTrigger.Once, TaskRunTrigger.ScheduleNow, TaskRunTrigger.Schedule];

    private static readonly DialogOptions dialogOptions = new() { MaxWidth = MaxWidth.Large, FullWidth = true, CloseOnEscapeKey = true };

    private readonly Dictionary<Guid, string> names = [];
    private IReadOnlyList<string> modules = [];
    private Func<string, TextDefinition?> moduleCaption = _ => null;
    private Func<int, string> recordsText = count => count.ToString(CultureInfo.InvariantCulture);
    private string? schema;
    private SerginAggregateList<TaskRunsListItem>? list;
    private TimeZoneInfo? timeZone;
    private PeriodicTimer? poller;
    private volatile bool disposed;

    private IReadOnlyList<Guid> actors = [];

    /// <summary>The schema asked for (the page's <c>?module=</c>); the first module when absent or not listed.</summary>
    [Parameter]
    public string? Module { get; set; }

    [Inject]
    private ISerginDispatcher Dispatcher { get; set; } = default!;

    [Inject]
    private IUiErrorPresenter ErrorPresenter { get; set; } = default!;

    [Inject]
    private IDialogService Dialogs { get; set; } = default!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = default!;

    [Inject]
    private IUserDirectory Users { get; set; } = default!;

    [Inject]
    private IUiTimeZoneStore TimeZones { get; set; } = default!;

    [Inject]
    private ICultureFormatter Formatter { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private TaskCatalog Catalog { get; set; } = default!;

    [Inject]
    private AggregateViewRegistry Views { get; set; } = default!;

    [Inject]
    private SerginUiModuleCatalog UiModules { get; set; } = default!;

    [Inject]
    private SerginScopeSession Scopes { get; set; } = default!;

    [Inject]
    private ILogger<SerginTaskRunsView> Logger { get; set; } = default!;

    /// <summary>The filter panel's Task field: a task's stored name.</summary>
    internal string? TaskFilter { get; set; }

    /// <summary>The filter panel's Status field.</summary>
    internal RunOutcome? StatusFilter { get; set; }

    /// <summary>The filter panel's Started by field.</summary>
    internal Guid? StartedByFilter { get; set; }

    /// <summary>The filter panel's Trigger field.</summary>
    internal TaskRunTrigger? TriggerFilter { get; set; }

    /// <summary>The filter panel's Started between field: whole days in the viewer's zone.</summary>
    internal DateRange? StartedFilter { get; set; }

    /// <summary>Whether the last load listed a running run, which keeps the poll reloading.</summary>
    internal bool HasRunning { get; private set; }

    /// <summary>Whether the poll timer runs (from the first render until disposal).</summary>
    internal bool IsPolling => poller is not null;

    private static IReadOnlyList<SerginBreadcrumb> Trail { get; } = [new(TaskUiTexts.RunList.Heading, SerginTaskRunsPage.Route)];

    private static IReadOnlyList<TaskRunTrigger> Triggers => triggers;

    // The tasks of the module shown, for the Task filter, by the text they are shown by.
    private IReadOnlyList<string> TaskNames =>
        [.. Catalog.Commands.Where(task => task.Schema == schema).Select(task => task.Name).OrderBy(TaskText, StringComparer.CurrentCulture)];

    private string? FirstModule => modules.Count > 0 ? modules[0] : null;

    private string AnyText => Localizer.Text(TaskUiTexts.RunList.AnyValue);

    protected override void OnInitialized()
    {
        modules = TaskRunRows.Modules(Catalog, UiModules);
        moduleCaption = ActivityRows.ModuleCaption(UiModules);
        recordsText = ActivityRows.RecordsText(Localizer, Formatter);
    }

    protected override void OnParametersSet()
    {
        string? selected = Module is { } asked && modules.Contains(asked, StringComparer.Ordinal) ? asked : FirstModule;

        if (!string.Equals(selected, schema, StringComparison.Ordinal))
        {
            // Another module: its list starts afresh (keyed on the schema), with none of the last module's filters.
            schema = selected;
            ClearFilters();
            actors = [];
            HasRunning = false;
        }
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            StartPolling();
        }
    }

    /// <summary>Loads the current page again; the header's reload icon, a poll tick and a cancel all come here.</summary>
    internal Task ReloadAsync() => disposed || list is null ? Task.CompletedTask : list.ReloadAsync();

    /// <summary>Starts the poll, which reloads every 5 seconds while the last load listed a running run.</summary>
    internal void StartPolling()
    {
        if (disposed || poller is not null || schema is null)
        {
            return;
        }

        poller = new PeriodicTimer(pollInterval);
        _ = PollAsync(poller);
    }

    /// <summary>Opens the run's records: what the task name links to.</summary>
    internal Task<IDialogReference> OpenRecordsAsync(TaskRunsListItem run)
    {
        DialogParameters<SerginRunItemsDialog> parameters = new()
        {
            { dialog => dialog.Schema, schema! },
            { dialog => dialog.Run, run },
        };

        return Dialogs.ShowAsync<SerginRunItemsDialog>(string.Empty, parameters, dialogOptions);
    }

    /// <summary>
    /// Cancels a running run, then reloads whatever the answer. A run that finished meanwhile is no error to the user (the
    /// reload shows how it ended); any other refusal is notified, and a send that throws says the run could not be
    /// cancelled rather than faulting the circuit.
    /// </summary>
    internal async Task CancelAsync(TaskRunsListItem run)
    {
        ArgumentNullException.ThrowIfNull(run);

        try
        {
            ErrorOr<Success> result = await Dispatcher.SendAsync(new CancelTaskRunCommand(schema!, run.Id));

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
    public ValueTask DisposeAsync()
    {
        disposed = true;
        poller?.Dispose();
        poller = null;

        return ValueTask.CompletedTask;
    }

    private async Task<ErrorOr<ListQueryResponse<TaskRunsListItem>>> LoadAsync(SerginListRequest request, CancellationToken cancellationToken)
    {
        timeZone ??= await TimeZones.GetTimeZoneAsync(cancellationToken);

        ErrorOr<ListQueryResponse<TaskRunsListItem>> result = await Dispatcher.SendAsync(
            new GetTaskRunsQuery(schema!, request.Paggination, request.Term, request.Filtering, request.Sorting), cancellationToken);

        if (!result.IsError)
        {
            HasRunning = result.Value.Data.Any(run => run.Outcome == RunOutcome.Running);
            await NameAsync([.. result.Value.Data.Select(run => run.ActingUserId)], cancellationToken);
        }

        return result;
    }

    // The people who started the module's runs, named, the first time the filter panel opens for this module.
    private async Task OnFilterPanelOpenedAsync()
    {
        timeZone ??= await TimeZones.GetTimeZoneAsync();

        if (actors.Count > 0)
        {
            return;
        }

        ErrorOr<IReadOnlyCollection<Guid>> result = await Dispatcher.SendAsync(new GetTaskRunActorsQuery(schema!));

        if (result.IsError)
        {
            ErrorPresenter.Notify(result.FirstError);
            return;
        }

        await NameAsync(result.Value, CancellationToken.None);
        actors = [.. result.Value.OrderBy(NameOf, StringComparer.CurrentCulture)];
    }

    private async Task NameAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        Guid[] unknown = [.. userIds.Distinct().Where(id => !names.ContainsKey(id))];

        if (unknown.Length == 0)
        {
            return;
        }

        foreach ((Guid id, string name) in await Users.GetDisplayNamesAsync(unknown, cancellationToken))
        {
            names[id] = name;
        }
    }

    /// <summary>The filters the panel's fields make, each with its chip: what Apply sends.</summary>
    internal IReadOnlyList<SerginAppliedFilter> BuildFilters()
    {
        List<SerginAppliedFilter> filters = [];

        if (TaskFilter is { } task)
        {
            filters.Add(new(TaskRunListFields.Task, FilteringType.equals, task, Chip(TaskTexts.RunList.Task, TaskText(task))));
        }

        if (StatusFilter is { } status)
        {
            filters.Add(new(TaskRunListFields.Status, FilteringType.equals, status.ToString(), Chip(TaskTexts.RunList.Status, OutcomeText(status))));
        }

        if (StartedByFilter is { } person)
        {
            filters.Add(new(TaskRunListFields.StartedBy, FilteringType.equals, person.ToString(), Chip(TaskTexts.RunList.StartedBy, NameOf(person))));
        }

        if (TriggerFilter is { } trigger)
        {
            filters.Add(new(
                TaskRunListFields.Trigger, FilteringType.equals, trigger.ToString(), Chip(TaskTexts.RunList.Trigger, Localizer.Text(TaskRunRows.TriggerText(trigger)))));
        }

        if (StartedFilter is { } range && (range.Start is not null || range.End is not null))
        {
            // Whole days in the viewer's zone: from the start of the first day to the start of the day after the last.
            string? from = range.Start is { } start ? ToUtcIso(start.Date) : null;
            string? to = range.End is { } end ? ToUtcIso(end.Date.AddDays(1)) : null;
            string days = Localizer.Text(ShellTexts.Lists.DateRange, new { From = DayLabel(range.Start), To = DayLabel(range.End) });

            filters.Add(new(TaskRunListFields.StartedAt, FilteringType.between, new[] { from, to }, Chip(TaskTexts.RunList.StartedAt, days)));
        }

        return [.. filters];
    }

    private void OnFilterRemoved(SerginAppliedFilter filter)
    {
        switch (filter.Id)
        {
            case TaskRunListFields.Task:
                TaskFilter = null;
                break;
            case TaskRunListFields.Status:
                StatusFilter = null;
                break;
            case TaskRunListFields.StartedBy:
                StartedByFilter = null;
                break;
            case TaskRunListFields.Trigger:
                TriggerFilter = null;
                break;
            default:
                StartedFilter = null;
                break;
        }
    }

    private void OnFiltersReset() => ClearFilters();

    private void ClearFilters()
    {
        TaskFilter = null;
        StatusFilter = null;
        StartedByFilter = null;
        TriggerFilter = null;
        StartedFilter = null;
    }

    private void OnModuleChanged(string? module) =>
        Navigation.NavigateTo(Navigation.GetUriWithQueryParameter(ModuleParameter, module));

    private async Task PollAsync(PeriodicTimer timer)
    {
        try
        {
            while (!disposed && await timer.WaitForNextTickAsync())
            {
                if (HasRunning)
                {
                    await InvokeAsync(ReloadAsync);
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A tick has no caller to observe its failure: it is logged and the poll ends; Reload still works.
            LogPollFailed(Logger, exception);
        }
    }

    private string ModuleText(string module) => moduleCaption(module) is { } caption ? Localizer.Text(caption) : module;

    private string TaskText(string commandName) => TaskRunRows.TaskText(Localizer, commandName, Catalog, Views);

    private string OutcomeText(RunOutcome outcome) => Localizer.Text(TaskTexts.RunList.Outcomes.For(outcome));

    private string NameOf(Guid userId) => TaskUiFormat.NameOf(names, userId);

    private string Time(DateTime utc) => TaskUiFormat.Time(Formatter, timeZone ?? TimeZoneInfo.Utc, utc);

    private string RecordsText(TaskRunsListItem run) =>
        TaskRunRows.RecordsText(run, recordsText, scope => TaskUiFormat.ScopeName(Scopes, scope));

    private string? RecordHref(TaskRunsListItem run) => ActivityRows.RecordHref(run.CommandName, run.Total, run.TargetId, Catalog, Views);

    private string SummaryText(TaskRunsListItem run) => TaskRunRows.Summary(Localizer, Formatter, run);

    private string Chip(FieldText field, string value) =>
        Localizer.Text(ShellTexts.Lists.FilterChip, new { Field = Localizer.Text(field.Label), Value = value });

    private string ToUtcIso(DateTime localDay) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDay, DateTimeKind.Unspecified), timeZone ?? TimeZoneInfo.Utc)
            .ToString("O", CultureInfo.InvariantCulture);

    // The picked day is a local calendar day, so it is shown as it is, without a zone shift.
    private string DayLabel(DateTime? day) => day is { } value ? Formatter.Date(new DateTimeOffset(value.Date, TimeSpan.Zero)) : "…";

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cancelling a task run from the Task runs page failed.")]
    private static partial void LogCancelFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The Task runs page's poll failed; it stops.")]
    private static partial void LogPollFailed(ILogger logger, Exception exception);
}
