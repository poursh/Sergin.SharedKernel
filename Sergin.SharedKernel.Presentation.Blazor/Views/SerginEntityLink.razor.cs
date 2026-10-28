using Microsoft.AspNetCore.Components;

namespace Sergin.SharedKernel.Presentation.Blazor.Views;

/// <summary>
/// The link from an entity list row to that entity's detail view. The list view cascades its
/// <see cref="SerginDetailOpenMode"/>; with <see cref="SerginDetailOpenMode.Page"/>, the only mode today, it is a plain
/// link to <see cref="Href"/>, the detail page's route.
/// </summary>
public sealed partial class SerginEntityLink
{
    /// <summary>The entity's detail page.</summary>
    [Parameter]
    [EditorRequired]
    public string Href { get; set; } = string.Empty;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>The enclosing list view's mode; read once a mode other than page exists.</summary>
    [CascadingParameter]
    public SerginDetailOpenMode Mode { get; set; }
}
