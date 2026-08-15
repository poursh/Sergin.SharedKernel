using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Presentation.Blazor.Modules;

namespace Sergin.SharedKernel.Presentation.Blazor.Layout;

public sealed partial class SerginNavMenu
{
    [Inject]
    private SerginUiModuleCatalog Catalog { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;
}
