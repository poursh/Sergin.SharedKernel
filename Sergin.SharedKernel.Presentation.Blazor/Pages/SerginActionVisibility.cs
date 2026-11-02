using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Domain.Securities;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// The one rule every action surface applies: an action whose permission the user lacks is hidden — on the current
/// scope, or on any scope for <see cref="SerginPageAction.AnyScope"/>. Presentation only; the pipeline still checks.
/// </summary>
/// <remarks>
/// One instance per component: answers are kept per permission and scope rule, so a re-render asks nothing new.
/// </remarks>
internal sealed class SerginActionVisibility(IScopeAuthorization scopeAuthorization)
{
    private readonly Dictionary<(PermissionDefinition Permission, bool AnyScope), bool> held = [];

    public async Task<IReadOnlyList<SerginPageAction>> VisibleAsync(IReadOnlyList<SerginPageAction> actions)
    {
        List<SerginPageAction> visible = new(actions.Count);

        foreach (SerginPageAction action in actions)
        {
            if (action.Permission is null || await HoldsAsync(action.Permission, action.AnyScope))
            {
                visible.Add(action);
            }
        }

        return visible;
    }

    private async Task<bool> HoldsAsync(PermissionDefinition permission, bool anyScope)
    {
        if (!held.TryGetValue((permission, anyScope), out bool holds))
        {
            holds = anyScope
                ? await scopeAuthorization.CurrentUserHoldsAnywhereAsync(permission)
                : await scopeAuthorization.CurrentUserHasAsync(permission);

            held[(permission, anyScope)] = holds;
        }

        return holds;
    }
}
