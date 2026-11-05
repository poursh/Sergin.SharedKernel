using Microsoft.AspNetCore.Components;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// One labelled field of a detail view: <see cref="Value"/> as text, or <see cref="ChildContent"/> for a link or
/// anything richer. An empty value shows a dash rather than nothing.
/// </summary>
public sealed partial class SerginDetailField
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
    public RenderFragment? ChildContent { get; set; }

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    private string LabelShown => Localizer.Show(LabelText, Label);
}
