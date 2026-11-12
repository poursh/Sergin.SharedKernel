using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// Draws one <see cref="SerginPageAction"/> as a button; when it carries menu items, as a split button whose
/// drop-down lists them. The action arrives already filtered by <see cref="SerginActionVisibility"/>.
/// </summary>
public sealed partial class SerginSplitButton
{
    [Parameter, EditorRequired]
    public SerginPageAction Action { get; set; } = default!;

    [Parameter]
    public Variant Variant { get; set; } = Variant.Outlined;

    [Parameter]
    public Color Color { get; set; } = Color.Primary;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;
}
