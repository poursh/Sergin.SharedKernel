using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// One figure of an aggregate page's overview: a label, the value, and a caption under it. An empty value shows a
/// dash while the figures load; the caption line is always rendered, so cards with and without one stay the same height.
/// </summary>
public sealed partial class SerginOverviewCard
{
    [Parameter]
    [EditorRequired]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public string? Caption { get; set; }

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;
}
