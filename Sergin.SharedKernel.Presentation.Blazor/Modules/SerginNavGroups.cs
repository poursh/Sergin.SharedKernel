using MudBlazor;
using Sergin.SharedKernel.Application.Localizations.Common;
using Sergin.SharedKernel.Modules;

namespace Sergin.SharedKernel.Presentation.Blazor.Modules;

/// <summary>
/// Drawer groups that several modules share: a module returns one as its <c>NavGroup</c> and the catalog folds
/// every module returning it into one section.
/// </summary>
/// <remarks>
/// Lives here rather than in <c>Sergin.SharedKernel.Modules</c> because the icon is a MudBlazor constant and
/// that project stays free of UI libraries.
/// </remarks>
public static class SerginNavGroups
{
    /// <summary>The administration section: users and access, settings.</summary>
    public static SerginNavGroup Administration { get; } = new(
        ShellTexts.Navigation.AdministrationGroup, Icons.Material.Filled.AdminPanelSettings, Order: 200);
}
