using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Application.Tasks;
using Sergin.SharedKernel.Application.Tasks.Queries;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.TimeZones;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Tasks.Runs;

/// <summary>The records of one run of the Task runs page, each named by the label stored with it.</summary>
public sealed partial class SerginRunItemsDialog
{
    private IReadOnlyCollection<TaskRunItemListItem> items = [];
    private TimeZoneInfo timeZone = TimeZoneInfo.Utc;
    private bool loading = true;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public string Schema { get; set; } = string.Empty;

    [Parameter]
    [EditorRequired]
    public TaskRunsListItem Run { get; set; } = default!;

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

    [Inject]
    private TaskCatalog Catalog { get; set; } = default!;

    [Inject]
    private AggregateViewRegistry Views { get; set; } = default!;

    private string TaskText => TaskRunRows.TaskText(Localizer, Run.CommandName, Catalog, Views);

    private string StartedText => TaskUiFormat.Time(Formatter, timeZone, Run.StartedAtUtc);

    private string TriggerText => TaskRunRows.TriggerLine(Localizer, Run);

    private string PersonText { get; set; } = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        timeZone = await TimeZones.GetTimeZoneAsync();
        PersonText = TaskUiFormat.NameOf(await Users.GetDisplayNamesAsync([Run.ActingUserId]), Run.ActingUserId);

        ErrorOr<IReadOnlyCollection<TaskRunItemListItem>> result = await Dispatcher.SendAsync(new GetTaskRunItemsQuery(Schema, Run.Id));

        if (result.IsError)
        {
            ErrorPresenter.Notify(result.Errors);
        }
        else
        {
            items = result.Value;
        }

        loading = false;

        // The heading is the dialog container's to render: it read the zone and the person before they were known.
        MudDialog.StateHasChanged();
    }

    private void Close() => MudDialog.Close();
}
