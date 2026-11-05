using Microsoft.AspNetCore.Components;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// One figure of an aggregate page's overview: a label, the value, and a caption under it. An empty value shows a
/// dash while the figures load; the caption line is always rendered, so cards with and without one stay the same height.
/// </summary>
public sealed partial class SerginOverviewCard
{

    /// <summary>The label, a declared text.</summary>
    [Parameter]
    [EditorRequired]
    public TextDefinition LabelText { get; set; } = default!;

    /// <summary>Data, not a label: the figure, already formatted through <c>ICultureFormatter</c>, shown as is.</summary>
    [Parameter]
    public string? ValueData { get; set; }

    /// <summary>Data, not a label: the line under the figure, already rendered from a declared text, shown as is.</summary>
    [Parameter]
    public string? CaptionData { get; set; }

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    private string LabelShown => Localizer.Text(LabelText);
}
