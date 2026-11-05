using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Presentation.Blazor.Navigation;
using Sergin.SharedKernel.Presentation.Blazor.Pages;
using Sergin.SharedKernel.Presentation.Errors;

namespace Sergin.SharedKernel.Presentation.Blazor.Views;

/// <summary>
/// The list view of one aggregate: overview, actions, then the table (<see cref="ChildContent"/> — a
/// <see cref="SerginAggregateList{TItem}"/> carrying its quick search, filter panel and bulk actions).
/// </summary>
public sealed partial class SerginAggregateListView
{
    /// <summary>Transitional: the title as a plain string, shown as written. <see cref="TitleText"/> wins when both are set.</summary>
    [Parameter]
    public string Title { get; set; } = string.Empty;

    /// <summary>The title, a declared text.</summary>
    [Parameter]
    public TextDefinition? TitleText { get; set; }

    [Parameter]
    public IReadOnlyList<SerginBreadcrumb> Trail { get; set; } = [];

    [Parameter]
    public RenderFragment? HeaderActions { get; set; }

    [Parameter]
    public SerginProblem? Problem { get; set; }

    [Parameter]
    public bool Ready { get; set; } = true;

    [Parameter]
    public RenderFragment? Overview { get; set; }

    [Parameter]
    public RenderFragment? OverviewSummary { get; set; }

    [Parameter]
    public bool OverviewCollapsed { get; set; }

    [Parameter]
    public IReadOnlyList<SerginPageAction> Actions { get; set; } = [];

    [Parameter]
    public int? Count { get; set; }

    [Parameter]
    public bool Collapsed { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
