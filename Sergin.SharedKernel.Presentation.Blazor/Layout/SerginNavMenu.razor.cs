using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Home;
using Sergin.SharedKernel.Presentation.Blazor.Modules;

namespace Sergin.SharedKernel.Presentation.Blazor.Layout;

/// <summary>
/// The drawer: the home entry, then one collapsible group per module, each holding that module's entries.
/// </summary>
public sealed partial class SerginNavMenu : IDisposable
{
    private readonly Dictionary<SerginNavGroup, bool> expanded = [];
    private IReadOnlyCollection<NavEntry> entries = [];

    [Inject]
    private SerginUiModuleCatalog Catalog { get; set; } = default!;

    [Inject]
    private SerginHome Home { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    [Inject]
    private IScopeAuthorization ScopeAuthorization { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    /// <summary>
    /// Builds the drawer once per circuit and opens the group of the page shown first.
    /// </summary>
    /// <remarks>
    /// A field filled here rather than a computed property, because the drawer re-renders on every
    /// navigation while this list only changes when the host is composed. Home is ordered among the groups by
    /// its own <see cref="SerginNavItem.Order"/> against each <see cref="SerginNavGroup.Order"/>, label against
    /// title on a tie, so a module can still sort itself above home. A group the user may use none of is left
    /// out entirely rather than shown empty.
    /// </remarks>
    protected override async Task OnInitializedAsync()
    {
        List<NavEntry> built = [];

        foreach (SerginNavSection section in Catalog.NavSections)
        {
            List<SerginNavItem> visible = [];

            foreach (SerginNavItem item in section.Items)
            {
                if (await IsVisibleAsync(item))
                {
                    visible.Add(item);
                }
            }

            if (visible.Count > 0)
            {
                built.Add(NavEntry.For(section with { Items = visible }));
            }
        }

        if (Home.NavItem is not null)
        {
            built.Add(NavEntry.For(Home.NavItem));
        }

        entries = [.. built.OrderBy(entry => entry.Order).ThenBy(entry => entry.Name, StringComparer.Ordinal)];

        ExpandGroupOf(Navigation.Uri);
        Navigation.LocationChanged += OnLocationChanged;
    }

    public void Dispose() => Navigation.LocationChanged -= OnLocationChanged;

    /// <summary>
    /// Hides an entry the current user cannot use. Presentation only — the handler behind the page
    /// enforces the same permission, so a hand-typed URL is refused whether or not the link showed.
    /// Shown when the user holds the permission on any scope, from stored grants rather than claims: the
    /// user may need the page in a scope other than the one the circuit works in now, and the page answers
    /// for the scope they pick, or, over data that belongs to no scope, for any scope. The host refused to start over
    /// an entry whose permission no aggregate registered, so every permission asked here is in the catalogue.
    /// </summary>
    private async Task<bool> IsVisibleAsync(SerginNavItem item)
    {
        if (item.RequiredPermission is null)
        {
            return true;
        }

        return await ScopeAuthorization.CurrentUserHoldsAnywhereAsync(item.RequiredPermission);
    }

    /// <summary>
    /// Prefix matching treats any href ending in a non-alphanumeric character as a prefix of everything
    /// below it, so the root href would light up on every page in the app. Only the root needs the
    /// exact-match rule; module hrefs still want prefix matching so detail pages keep their section lit.
    /// </summary>
    private static NavLinkMatch MatchFor(SerginNavItem item)
        => item.Href is SerginHome.RootPath ? NavLinkMatch.All : NavLinkMatch.Prefix;

    private bool IsExpanded(SerginNavGroup group) => expanded.GetValueOrDefault(group);

    private EventCallback<bool> ExpandedChangedFor(SerginNavGroup group)
        => EventCallback.Factory.Create<bool>(this, isExpanded => expanded[group] = isExpanded);

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        ExpandGroupOf(e.Location);
        _ = InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Opens the group holding the entry the page at <paramref name="uri"/> lights up, under the same prefix rule
    /// as <see cref="NavLinkMatch.Prefix"/>. It only ever opens: a group the user opened stays open when they
    /// navigate elsewhere, and a group they closed reopens only when they navigate into it.
    /// </summary>
    private void ExpandGroupOf(string uri)
    {
        string relative = Navigation.ToBaseRelativePath(uri);
        int queryOrFragment = relative.IndexOfAny(['?', '#']);
        string path = "/" + (queryOrFragment < 0 ? relative : relative[..queryOrFragment]);

        foreach (NavEntry entry in entries)
        {
            if (entry.Section is { } section && section.Items.Any(item => IsAtOrBelow(path, item.Href)))
            {
                expanded[section.Group] = true;
            }
        }
    }

    private static bool IsAtOrBelow(string path, string href)
        => path.Equals(href, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(href.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A top-level drawer entry: either a module's group or the home link.
    /// </summary>
    private sealed record NavEntry(int Order, string Name, SerginNavSection? Section, SerginNavItem? Link)
    {
        public static NavEntry For(SerginNavSection section)
            => new(section.Group.Order, section.Group.Title, section, null);

        public static NavEntry For(SerginNavItem link) => new(link.Order, link.Label, null, link);
    }
}
