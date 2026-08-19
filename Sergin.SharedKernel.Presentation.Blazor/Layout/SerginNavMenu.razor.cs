using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Home;
using Sergin.SharedKernel.Presentation.Blazor.Modules;

namespace Sergin.SharedKernel.Presentation.Blazor.Layout;

public sealed partial class SerginNavMenu
{
    private IReadOnlyCollection<SerginNavItem> navItems = [];

    [Inject]
    private SerginUiModuleCatalog Catalog { get; set; } = default!;

    [Inject]
    private SerginHome Home { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    /// <summary>
    /// Merges the home entry into the modules' entries once per circuit.
    /// </summary>
    /// <remarks>
    /// A field filled here rather than a computed property, because the drawer re-renders on every
    /// navigation while this list only changes when the host is composed. The comparator repeats
    /// <c>SerginUiModuleCatalog</c>'s — order first, then label, ordinal — so a module can still sort
    /// itself above home. It is repeated rather than pushed into the catalog because that type is the
    /// <em>module</em> catalog and home is not a module.
    /// </remarks>
    protected override void OnInitialized()
    {
        IEnumerable<SerginNavItem> items = Catalog.NavItems;

        if (Home.NavItem is not null)
        {
            items = items.Prepend(Home.NavItem);
        }

        navItems = [.. items.OrderBy(item => item.Order).ThenBy(item => item.Label, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Prefix matching treats any href ending in a non-alphanumeric character as a prefix of everything
    /// below it, so the root href would light up on every page in the app. Only the root needs the
    /// exact-match rule; module hrefs still want prefix matching so detail pages keep their section lit.
    /// </summary>
    private static NavLinkMatch MatchFor(SerginNavItem item)
        => item.Href is SerginHome.RootPath ? NavLinkMatch.All : NavLinkMatch.Prefix;
}
