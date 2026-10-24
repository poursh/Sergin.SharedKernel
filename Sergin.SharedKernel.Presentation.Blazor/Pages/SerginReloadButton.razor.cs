using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>The page header's reload icon: runs the page's own reload, nothing else.</summary>
public sealed partial class SerginReloadButton
{
    [Parameter]
    [EditorRequired]
    public EventCallback OnReload { get; set; }

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;
}
