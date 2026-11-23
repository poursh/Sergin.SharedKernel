using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Commands.Queries;
using Sergin.SharedKernel.Application.Tasks.Queries;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Domain.Tasks;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Localizations;
using Sergin.SharedKernel.Presentation.Blazor.TimeZones;

namespace Sergin.SharedKernel.Presentation.Blazor.Tasks;

/// <summary>
/// A schedule's recent runs, newest first, and — for the run the user opens — its items with their errors: which record
/// succeeded, which failed, how many attempts it took and what the last one said (<see cref="Runs.SerginRunItemsTable"/>).
/// </summary>
public sealed partial class SerginTaskRunsDialog
{
    private const int PageSize = 20;

    private IReadOnlyCollection<TaskRunListItem> runs = [];
    private IReadOnlyCollection<TaskRunItemListItem> items = [];
    private IReadOnlyDictionary<Guid, string> names = new Dictionary<Guid, string>();
    private TimeZoneInfo timeZone = TimeZoneInfo.Utc;
    private Guid? openRun;
    private Guid? openRunScope;
    private bool loading = true;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public string Schema { get; set; } = string.Empty;

    [Parameter]
    [EditorRequired]
    public TaskScheduleId ScheduleId { get; set; } = default!;

    [Parameter]
    public string CommandName { get; set; } = string.Empty;

    [Inject]
    private ISerginDispatcher Dispatcher { get; set; } = default!;

    [Inject]
    private IUiErrorPresenter ErrorPresenter { get; set; } = default!;

    [Inject]
    private IUserDirectory Users { get; set; } = default!;

    [Inject]
    private IUiTimeZoneStore TimeZones { get; set; } = default!;

    [Inject]
    private ICultureFormatter Formatter { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        timeZone = await TimeZones.GetTimeZoneAsync();

        ErrorOr<ListQueryResponse<TaskRunListItem>> result =
            await Dispatcher.SendAsync(new GetTaskScheduleRunsQuery(Schema, ScheduleId, Paggination.Create(PageSize, 1)));

        if (result.IsError)
        {
            ErrorPresenter.Notify(result.Errors);
        }
        else
        {
            runs = result.Value.Data;
            names = await Users.GetDisplayNamesAsync([.. runs.Select(run => run.ActingUserId).Distinct()]);
        }

        loading = false;
    }

    internal async Task ShowItemsAsync(Guid runId)
    {
        ErrorOr<IReadOnlyCollection<TaskRunItemListItem>> result =
            await Dispatcher.SendAsync(new GetTaskRunItemsQuery(Schema, runId));

        if (result.IsError)
        {
            ErrorPresenter.Notify(result.Errors);
            return;
        }

        items = result.Value;
        openRun = runId;
        openRunScope = runs.FirstOrDefault(run => run.Id == runId)?.ScopeId;

        // Not only a click handler calls it (a test does), so it re-renders on its own.
        StateHasChanged();
    }

    private string Time(DateTime utc) => TaskUiFormat.Time(Formatter, timeZone, utc);

    private string NameOf(Guid userId) => TaskUiFormat.NameOf(names, userId);

    private string TriggerText(TaskRunTrigger trigger) => Localizer.Text(TaskUiFormat.TriggerText(trigger));

    private string SummaryText(TaskRunListItem run) => TaskUiFormat.RunSummary(Localizer, Formatter, run);

    private string Heading => Localizer.Text(TaskUiTexts.Runs.Heading, new { Command = CommandName });

    private void Close() => MudDialog.Close();
}
