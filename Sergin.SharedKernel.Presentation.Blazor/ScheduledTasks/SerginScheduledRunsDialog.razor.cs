using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Commands.Queries;
using Sergin.SharedKernel.Application.ScheduledTasks.Queries;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Domain.ScheduledTasks;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Times;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.ScheduledTasks;

/// <summary>
/// A schedule's recent runs, newest first, and — for the run the user opens — its items with their errors: which record
/// succeeded, which failed, how many attempts it took and what the last one said.
/// </summary>
public sealed partial class SerginScheduledRunsDialog
{
    private const int PageSize = 20;

    private IReadOnlyCollection<ScheduledRunListItem> runs = [];
    private IReadOnlyCollection<ScheduledRunItemListItem> items = [];
    private IReadOnlyDictionary<Guid, string> names = new Dictionary<Guid, string>();
    private TimeZoneInfo timeZone = TimeZoneInfo.Utc;
    private Guid? openRun;
    private bool loading = true;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public string Schema { get; set; } = string.Empty;

    [Parameter]
    [EditorRequired]
    public ScheduledTaskId TaskId { get; set; } = default!;

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

        ErrorOr<ListQueryResponse<ScheduledRunListItem>> result =
            await Dispatcher.SendAsync(new GetScheduledTaskRunsQuery(Schema, TaskId, Paggination.Create(PageSize, 1)));

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

    private async Task ShowItemsAsync(ScheduledRunListItem run)
    {
        ErrorOr<IReadOnlyCollection<ScheduledRunItemListItem>> result =
            await Dispatcher.SendAsync(new GetScheduledTaskRunItemsQuery(Schema, run.Id));

        if (result.IsError)
        {
            ErrorPresenter.Notify(result.Errors);
            return;
        }

        items = result.Value;
        openRun = run.Id;
    }

    private string Time(DateTime utc) =>
        Formatter.DateTime(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)), timeZone);

    private string NameOf(Guid userId) => names.GetValueOrDefault(userId) ?? userId.ToString("N")[..8];

    private string TriggerText(ScheduledRunTrigger trigger) =>
        Localizer.Text(trigger switch
        {
            ScheduledRunTrigger.Scheduled => ScheduledTaskUiTexts.Runs.Scheduled,
            ScheduledRunTrigger.RunNow => ScheduledTaskUiTexts.Runs.RunNowTrigger,
            _ => ScheduledTaskUiTexts.Runs.OneOff,
        });

    private string StatusText(ScheduledRunStatus status) =>
        Localizer.Text(status switch
        {
            ScheduledRunStatus.Running => ScheduledTaskUiTexts.Runs.Running,
            ScheduledRunStatus.Completed => ScheduledTaskUiTexts.Runs.Completed,
            _ => ScheduledTaskUiTexts.Runs.Skipped,
        });

    private string ItemStatusText(ScheduledRunItemStatus status) =>
        Localizer.Text(status switch
        {
            ScheduledRunItemStatus.Pending => ScheduledTaskUiTexts.Runs.ItemPending,
            ScheduledRunItemStatus.Running => ScheduledTaskUiTexts.Runs.ItemRunning,
            ScheduledRunItemStatus.Retrying => ScheduledTaskUiTexts.Runs.ItemRetrying,
            ScheduledRunItemStatus.Succeeded => ScheduledTaskUiTexts.Runs.ItemSucceeded,
            _ => ScheduledTaskUiTexts.Runs.ItemFailed,
        });

    private string ResultText(ScheduledRunListItem run) =>
        Localizer.Text(
            ScheduledTaskUiTexts.Runs.Result,
            new { Succeeded = Formatter.Number(run.Succeeded), Total = Formatter.Number(run.Total), Failed = Formatter.Number(run.Failed) });

    private void Close() => MudDialog.Close();
}
