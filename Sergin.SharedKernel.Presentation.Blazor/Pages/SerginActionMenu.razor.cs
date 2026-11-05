using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Securities.Authorization;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// A menu of <see cref="SerginPageAction"/>s behind one icon button — a tree node's ⋮. Hides the actions whose
/// permission the user lacks, by the same rule as <see cref="SerginActionBar"/>.
/// </summary>
public sealed partial class SerginActionMenu
{
    private SerginActionVisibility visibility = default!;
    private IReadOnlyList<SerginPageAction> visibleActions = [];

    [Parameter]
    public IReadOnlyList<SerginPageAction> Actions { get; set; } = [];

    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.MoreVert;

    /// <summary>Transitional: the button's accessible name as a plain string, shown as written.</summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>The button's accessible name; the shell's <c>Actions</c> when neither this nor <see cref="AriaLabel"/> is set.</summary>
    [Parameter]
    public TextDefinition? AriaLabelText { get; set; }

    [Parameter]
    public string? Class { get; set; }

    [Inject]
    private IScopeAuthorization ScopeAuthorization { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    private string AriaLabelShown => Localizer.Show(AriaLabelText, AriaLabel, ShellTexts.Navigation.Actions);

    protected override void OnInitialized() => visibility = new SerginActionVisibility(ScopeAuthorization);

    protected override async Task OnParametersSetAsync() => visibleActions = await visibility.VisibleAsync(Actions);
}
