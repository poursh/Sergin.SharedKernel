using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Presentation.Blazor.Home;
using Sergin.SharedKernel.Presentation.Blazor.Modules;

namespace Sergin.SharedKernel.Presentation.Blazor.Navigation;

/// <summary>
/// The header row every routable page renders: the breadcrumb trail
/// (<c>Home &gt; Module &gt; Section &gt; … &gt; current page</c>) on the left, the page's icon buttons
/// (<see cref="ChildContent"/> — reload, copy link, audit, help) on the right, and a rule under both.
/// </summary>
/// <remarks>
/// The page passes only its own steps through <see cref="Trail"/>; the home step is prepended here from
/// <see cref="SerginHome.NavItem"/>, under the same rule as the drawer — a host that called
/// <c>WithoutNavItem()</c> gets no home crumb either. The module step follows it, from the drawer group of the
/// module whose nav entry the trail's first step links to; a trail starting anywhere else gets none. The last step is the current page and is rendered
/// as disabled text whatever href it carries, so no page has to remember to leave it out.
/// </remarks>
public sealed partial class SerginPageHeader
{
    private IReadOnlyList<BreadcrumbItem> items = [];
    private BreadcrumbItem? current;

    /// <summary>
    /// The page's steps below home, first to last; the last one is the current page.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<SerginBreadcrumb> Trail { get; set; } = [];

    /// <summary>The page's icon buttons, right-aligned; none for a page with nothing to offer there.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Inject]
    private SerginHome Home { get; set; } = default!;

    [Inject]
    private SerginUiModuleCatalog Catalog { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    protected override void OnParametersSet()
    {
        List<BreadcrumbItem> built = new(Trail.Count + 2);

        if (Home.NavItem is { } home)
        {
            built.Add(new BreadcrumbItem(Localizer[home.Label], home.Href, icon: home.Icon));
        }

        // The module step: a page's first step is its section, taken from a module's nav entry, so the module
        // that entry belongs to is known here and no page names it. Not a link — a module has no page of its own.
        if (Trail.Count > 0 && Trail[0].Href is { } sectionHref && Catalog.GroupOf(sectionHref) is { } group)
        {
            built.Add(new BreadcrumbItem(Localizer[group.Title], href: null, disabled: true));
        }

        for (int index = 0; index < Trail.Count; index++)
        {
            SerginBreadcrumb crumb = Trail[index];

            // The href is dropped, not just disabled: MudBlazor keeps a disabled item's href on the anchor,
            // and the current page should not be a link to itself in any reading of the markup.
            string? href = index == Trail.Count - 1 ? null : crumb.Href;

            built.Add(new BreadcrumbItem(Localizer[crumb.Label], href, disabled: href is null));
        }

        items = built;
        current = built.Count > 0 ? built[^1] : null;
    }

    // Null leaves the attribute out; MudBlazor's default template marks only the trail's last item.
    private string? AriaCurrent(BreadcrumbItem item) => ReferenceEquals(item, current) ? "page" : null;
}
