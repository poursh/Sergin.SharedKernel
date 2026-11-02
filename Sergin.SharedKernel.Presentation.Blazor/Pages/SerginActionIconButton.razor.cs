using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Application.Securities.Authorization;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// A <see cref="SerginPageAction"/> as a small icon button — a tree group header's <c>+</c>. Hidden when the user
/// lacks its permission, by the same rule as <see cref="SerginActionBar"/>.
/// </summary>
public sealed partial class SerginActionIconButton
{
    private SerginActionVisibility visibility = default!;
    private bool visible;

    [Parameter]
    [EditorRequired]
    public SerginPageAction Action { get; set; } = default!;

    [Inject]
    private IScopeAuthorization ScopeAuthorization { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    protected override void OnInitialized() => visibility = new SerginActionVisibility(ScopeAuthorization);

    protected override async Task OnParametersSetAsync() => visible = (await visibility.VisibleAsync([Action])).Count > 0;
}
