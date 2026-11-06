using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Securities.Authorization;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// A row of <see cref="SerginPageAction"/> buttons, filtered by permission and laid out by kind — the actions bar of
/// <see cref="SerginAggregatePage"/> and of <see cref="SerginEntityListSection"/>.
/// </summary>
/// <remarks>
/// The owner computes its actions from its own state and passes them in on every render; this component only hides
/// the ones whose permission the user lacks (<see cref="SerginActionVisibility"/>) and lays the rest out.
/// </remarks>
public sealed partial class SerginActionBar
{
    private SerginActionVisibility visibility = default!;
    private IReadOnlyList<SerginPageAction> visibleActions = [];

    [Parameter]
    public IReadOnlyList<SerginPageAction> Actions { get; set; } = [];

    /// <summary>Extra classes on the bar, e.g. its margin.</summary>
    [Parameter]
    public string? Class { get; set; }

    [Inject]
    private IScopeAuthorization ScopeAuthorization { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    protected override void OnInitialized() => visibility = new SerginActionVisibility(ScopeAuthorization);

    protected override async Task OnParametersSetAsync() => visibleActions = await visibility.VisibleAsync(Actions);
}
