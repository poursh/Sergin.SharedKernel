using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Presentation.Blazor.Navigation;
using Sergin.SharedKernel.Presentation.Blazor.Pages;
using Sergin.SharedKernel.Presentation.Errors;

namespace Sergin.SharedKernel.Presentation.Blazor.Views;

/// <summary>
/// The list view of a collection of entities inside one aggregate: overview, actions, then the table
/// (<see cref="ChildContent"/> — a <see cref="SerginEntityList{TItem}"/>). Embedded in its root's detail view it is
/// an outlined, titled section with the count.
/// </summary>
/// <remarks>
/// <see cref="DetailMode"/> is cascaded to the rows' <see cref="SerginEntityLink"/>s, so the list, not each row, says
/// how an entity's detail opens. Its actions are the root's behaviours on the collection: guarded by the root's
/// permissions and sent at the root's row version.
/// </remarks>
public sealed partial class SerginEntityListView
{
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

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

    /// <summary>How a row's link opens the entity's detail view.</summary>
    [Parameter]
    public SerginDetailOpenMode DetailMode { get; set; } = SerginDetailOpenMode.Page;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
