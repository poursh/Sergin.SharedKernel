using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Presentation.Blazor.Navigation;
using Sergin.SharedKernel.Presentation.Blazor.Pages;
using Sergin.SharedKernel.Presentation.Errors;

namespace Sergin.SharedKernel.Presentation.Blazor.Views;

/// <summary>
/// The shape every aggregate and entity view shares: an optional collapsible <see cref="Overview"/>, the
/// <see cref="Actions"/> bar, then the body (<see cref="ChildContent"/>), plus the view's own title, trail, header
/// buttons and problem.
/// </summary>
/// <remarks>
/// A module view (<c>DeviceDetailView</c>) is smart: it loads its record, holds its version, builds its actions and
/// passes all of it here. Where the parts land is the host's: inside a <see cref="SerginPage"/> the title becomes the
/// page title and <see cref="Trail"/>/<see cref="HeaderActions"/> the header row; embedded, the view is an outlined
/// section titled <see cref="Title"/> with <see cref="Count"/>, and the trail and header buttons are not shown. The
/// typed views (<see cref="SerginAggregateListView"/>, <see cref="SerginAggregateDetailView"/>,
/// <see cref="SerginEntityListView"/>, <see cref="SerginEntityDetailView"/>) wrap this one.
/// </remarks>
public sealed partial class SerginView
{
    private SerginViewHost bodyHost = default!;
    private bool collapsed;

    /// <summary>The page title on a page; the section title (localised) when embedded.</summary>
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>The view's steps below home, first to last; shown only on a page.</summary>
    [Parameter]
    public IReadOnlyList<SerginBreadcrumb> Trail { get; set; } = [];

    /// <summary>The header's icon buttons — reload, copy link, audit, help; shown only on a page.</summary>
    [Parameter]
    public RenderFragment? HeaderActions { get; set; }

    /// <summary>What went wrong loading the view; shown above it.</summary>
    [Parameter]
    public SerginProblem? Problem { get; set; }

    /// <summary>Whether the parts render; false until the record is loaded, so only the header and problem show.</summary>
    [Parameter]
    public bool Ready { get; set; } = true;

    /// <summary>The KPI content shown expanded; null for a view with no overview.</summary>
    [Parameter]
    public RenderFragment? Overview { get; set; }

    /// <summary>The one-line form of the overview shown while it is collapsed.</summary>
    [Parameter]
    public RenderFragment? OverviewSummary { get; set; }

    /// <summary>Whether the overview starts collapsed.</summary>
    [Parameter]
    public bool OverviewCollapsed { get; set; }

    [Parameter]
    public IReadOnlyList<SerginPageAction> Actions { get; set; } = [];

    /// <summary>The number of records, shown after the title when embedded; null until known.</summary>
    [Parameter]
    public int? Count { get; set; }

    /// <summary>Whether an embedded view starts collapsed: the title and count only.</summary>
    [Parameter]
    public bool Collapsed { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [CascadingParameter]
    private SerginViewHost Host { get; set; } = SerginViewHost.Unhosted;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    protected override void OnInitialized()
    {
        collapsed = Collapsed;
        bodyHost = Host.Embedded();
    }

    private void Toggle() => collapsed = !collapsed;
}
