using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Presentation.Blazor.Navigation;
using Sergin.SharedKernel.Presentation.Blazor.Pages;
using Sergin.SharedKernel.Presentation.Errors;

namespace Sergin.SharedKernel.Presentation.Blazor.Views;

/// <summary>
/// The detail view of one aggregate: overview, actions, the record's <see cref="Fields"/>, then its
/// <see cref="Entities"/> — the collections inside it, each an embedded <see cref="SerginEntityListView"/>.
/// </summary>
public sealed partial class SerginAggregateDetailView
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

    /// <summary>The heading over the fields, usually the record's name — user data, shown as is.</summary>
    [Parameter]
    public string? FieldsTitle { get; set; }

    /// <summary>The heading over the fields as a declared text; wins over <see cref="FieldsTitle"/>.</summary>
    [Parameter]
    public TextDefinition? FieldsTitleText { get; set; }

    /// <summary>The record's labelled fields: <c>SerginDetailField</c>s.</summary>
    [Parameter]
    public RenderFragment? Fields { get; set; }

    /// <summary>The collections of entities inside the record, each an embedded list view.</summary>
    [Parameter]
    public RenderFragment? Entities { get; set; }
}
