using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// The page header's help icon, opening <see cref="Href"/> in a new tab. Renders nothing without one, so a page can
/// declare it before its help exists.
/// </summary>
public sealed partial class SerginHelpButton
{
    [Parameter]
    public string? Href { get; set; }

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;
}
