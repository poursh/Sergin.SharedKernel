using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Domain.Securities;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// A row of <see cref="SerginPageAction"/> buttons, filtered by permission and laid out by kind — the actions bar of
/// <see cref="SerginAggregatePage"/> and of <see cref="SerginEntityListSection"/>.
/// </summary>
/// <remarks>
/// The owner computes its actions from its own state and passes them in on every render; this component only hides
/// the ones whose permission the user lacks and lays the rest out. Permission answers are kept per permission and
/// scope rule, so a re-render asks nothing new.
/// </remarks>
public sealed partial class SerginActionBar
{
    private readonly Dictionary<(PermissionDefinition Permission, bool AnyScope), bool> held = [];
    private IReadOnlyList<SerginPageAction> visibleActions = [];

    [Parameter]
    public IReadOnlyList<SerginPageAction> Actions { get; set; } = [];

    /// <summary>Extra classes on the bar, e.g. its margin.</summary>
    [Parameter]
    public string? Class { get; set; }

    [Inject]
    private IScopeAuthorization ScopeAuthorization { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        List<SerginPageAction> visible = new(Actions.Count);

        foreach (SerginPageAction action in Actions)
        {
            if (action.Permission is null || await HoldsAsync(action.Permission, action.AnyScope))
            {
                visible.Add(action);
            }
        }

        visibleActions = visible;
    }

    private async Task<bool> HoldsAsync(PermissionDefinition permission, bool anyScope)
    {
        if (!held.TryGetValue((permission, anyScope), out bool holds))
        {
            holds = anyScope
                ? await ScopeAuthorization.CurrentUserHoldsAnywhereAsync(permission)
                : await ScopeAuthorization.CurrentUserHasAsync(permission);

            held[(permission, anyScope)] = holds;
        }

        return holds;
    }
}
