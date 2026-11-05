using Microsoft.AspNetCore.Components;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// One figure of an aggregate page's overview: a label, the value, and a caption under it. An empty value shows a
/// dash while the figures load; the caption line is always rendered, so cards with and without one stay the same height.
/// </summary>
public sealed partial class SerginOverviewCard
{
    /// <summary>Transitional: the label as a plain string, shown as written. <see cref="LabelText"/> wins when both are set.</summary>
    [Parameter]
    public string Label { get; set; } = string.Empty;

    /// <summary>The label, a declared text.</summary>
    [Parameter]
    public TextDefinition? LabelText { get; set; }

    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public string? Caption { get; set; }

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    private string LabelShown => Localizer.Show(LabelText, Label);
}
