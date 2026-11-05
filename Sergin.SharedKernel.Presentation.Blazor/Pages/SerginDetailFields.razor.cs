using Microsoft.AspNetCore.Components;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>The body of an aggregate detail page: the record's name as a heading over its <see cref="SerginDetailField"/>s.</summary>
public sealed partial class SerginDetailFields
{
    /// <summary>The heading, usually the record's name — user data, shown as is.</summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>The heading as a declared text; wins over <see cref="Title"/>.</summary>
    [Parameter]
    public TextDefinition? TitleText { get; set; }

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    private string? TitleShown => TitleText is null ? Title : Localizer.Text(TitleText);

    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
