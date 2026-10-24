using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// One labelled field of a detail view: <see cref="Value"/> as text, or <see cref="ChildContent"/> for a link or
/// anything richer. An empty value shows a dash rather than nothing.
/// </summary>
public sealed partial class SerginDetailField
{
    [Parameter]
    [EditorRequired]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;
}
