using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Tasks.Queries;
using Sergin.SharedKernel.Presentation.Blazor.Scopes;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Tasks.Runs;

/// <summary>
/// The item table of one run, shared by the schedule's runs dialog and the Task runs page's records dialog. A record is
/// named by the label stored with it, else its id; a collection task's item, which has none, names the run's scope.
/// </summary>
public sealed partial class SerginRunItemsTable
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyCollection<TaskRunItemListItem> Items { get; set; } = [];

    /// <summary>The scope of the run, for a collection task's run; null otherwise.</summary>
    [Parameter]
    public Guid? RunScope { get; set; }

    /// <summary>The viewer's zone, which adds a column of when each record finished; null leaves it out.</summary>
    [Parameter]
    public TimeZoneInfo? FinishedIn { get; set; }

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    [Inject]
    private ICultureFormatter Formatter { get; set; } = default!;

    [Inject]
    private SerginScopeSession Scopes { get; set; } = default!;

    private string TargetText(TaskRunItemListItem item) =>
        item.TargetLabel ?? item.TargetId?.ToString() ?? (RunScope is { } scope ? TaskUiFormat.ScopeName(Scopes, scope) : string.Empty);

    private string ItemStatusText(TaskRunItemListItem item) => TaskUiFormat.ItemStatus(Localizer, Formatter, item);

    private string? FinishedText(TaskRunItemListItem item, TimeZoneInfo zone) =>
        item.CompletedAtUtc is { } at ? TaskUiFormat.Time(Formatter, zone, at) : null;
}
