using Microsoft.AspNetCore.Components;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>The body of an aggregate detail page: the record's name as a heading over its <see cref="SerginDetailField"/>s.</summary>
public sealed partial class SerginDetailFields
{
    /// <summary>Data, not a label: the heading as the user stored it, usually the record's name, shown as is.</summary>
    [Parameter]
    public string? TitleData { get; set; }

    /// <summary>The heading as a declared text; wins over <see cref="TitleData"/>.</summary>
    [Parameter]
    public TextDefinition? TitleText { get; set; }

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    private string? TitleShown => TitleText is null ? TitleData : Localizer.Text(TitleText);

    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
