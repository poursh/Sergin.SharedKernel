using Microsoft.AspNetCore.Components;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>The body of an aggregate detail page: the record's name as a heading over its <see cref="SerginDetailField"/>s.</summary>
public sealed partial class SerginDetailFields
{
    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
