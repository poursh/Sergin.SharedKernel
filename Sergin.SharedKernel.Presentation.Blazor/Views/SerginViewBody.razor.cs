using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Presentation.Blazor.Pages;

namespace Sergin.SharedKernel.Presentation.Blazor.Views;

/// <summary>
/// The parts of a <see cref="SerginView"/> — overview, actions bar, body — drawn the same way on a page and embedded,
/// so the two shapes differ only in what surrounds them.
/// </summary>
public sealed partial class SerginViewBody
{
    private bool collapsed;

    [Parameter]
    public RenderFragment? Overview { get; set; }

    [Parameter]
    public RenderFragment? OverviewSummary { get; set; }

    [Parameter]
    public bool OverviewCollapsed { get; set; }

    [Parameter]
    public IReadOnlyList<SerginPageAction> Actions { get; set; } = [];

    /// <summary>The actions bar's margin, which differs between the two shapes.</summary>
    [Parameter]
    public string? ActionsClass { get; set; }

    /// <summary>The host cascaded to the body: always embedded.</summary>
    [Parameter]
    [EditorRequired]
    public SerginViewHost Host { get; set; } = SerginViewHost.Unhosted;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    protected override void OnInitialized() => collapsed = OverviewCollapsed;

    private void ToggleOverview() => collapsed = !collapsed;
}
