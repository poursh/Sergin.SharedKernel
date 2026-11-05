using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Presentation.Blazor.Navigation;
using Sergin.SharedKernel.Presentation.Blazor.Pages;
using Sergin.SharedKernel.Presentation.Errors;

namespace Sergin.SharedKernel.Presentation.Blazor.Views;

/// <summary>The detail view of one entity inside an aggregate: overview, actions, then the entity's <see cref="Fields"/>.</summary>
public sealed partial class SerginEntityDetailView
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
}
