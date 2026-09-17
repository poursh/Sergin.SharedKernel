using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Presentation.Blazor.Home;

namespace Sergin.SharedKernel.Presentation.Blazor.Navigation;

/// <summary>
/// The breadcrumb strip every routable page renders: <c>Home &gt; Section &gt; … &gt; current page</c>.
/// </summary>
/// <remarks>
/// The page passes only its own steps through <see cref="Trail"/>; the home step is prepended here from
/// <see cref="SerginHome.NavItem"/>, under the same rule as the drawer — a host that called
/// <c>WithoutNavItem()</c> gets no home crumb either. The last step is the current page and is rendered
/// as disabled text whatever href it carries, so no page has to remember to leave it out.
/// </remarks>
public sealed partial class SerginBreadcrumbs
{
    private IReadOnlyList<BreadcrumbItem> items = [];

    /// <summary>
    /// Identifies the outlet in <c>SerginMainLayout</c> this component projects into. An object rather than a
    /// section name, so no other section can collide with it by spelling the same string.
    /// </summary>
    public static object SectionId { get; } = new();

    /// <summary>
    /// The page's steps below home, first to last; the last one is the current page.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<SerginBreadcrumb> Trail { get; set; } = [];

    [Inject]
    private SerginHome Home { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    protected override void OnParametersSet()
    {
        List<BreadcrumbItem> built = new(Trail.Count + 1);

        if (Home.NavItem is { } home)
        {
            built.Add(new BreadcrumbItem(Localizer[home.Label], home.Href));
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
    }
}
