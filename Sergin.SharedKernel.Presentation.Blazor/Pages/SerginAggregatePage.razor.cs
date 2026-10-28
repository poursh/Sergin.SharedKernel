using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// The default page for one aggregate: an optional <see cref="Overview"/>, the <see cref="Actions"/> bar, then the
/// body (<see cref="ChildContent"/> — a <c>SerginAggregateList</c> or a detail view).
/// </summary>
/// <remarks>
/// The page computes its actions from its own state — a list's actions, a detail page's actions once the record is
/// loaded — and passes them in on every render; <see cref="SerginActionBar"/> filters them by permission and lays
/// them out.
/// <para>Superseded by <see cref="Views.SerginAggregateListView"/> and <see cref="Views.SerginAggregateDetailView"/>, which a page hosts in <see cref="Views.SerginPage"/>; kept until UserAccess's
/// pages move to views.</para>
/// </remarks>
public sealed partial class SerginAggregatePage
{
    private bool collapsed;

    /// <summary>The KPI content shown expanded; null for an aggregate with no overview.</summary>
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

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    protected override void OnInitialized() => collapsed = OverviewCollapsed;

    private void ToggleOverview() => collapsed = !collapsed;
}
