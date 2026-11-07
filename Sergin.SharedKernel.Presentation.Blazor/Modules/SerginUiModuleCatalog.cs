using System.Reflection;
using Sergin.SharedKernel.Modules;

namespace Sergin.SharedKernel.Presentation.Blazor.Modules;

public sealed class SerginUiModuleCatalog
{
    public SerginUiModuleCatalog(IReadOnlyCollection<ISerginWebUiModule> modules)
    {
        Modules = modules;
        RoutableAssemblies = [.. modules.Select(module => module.UiAssembly), typeof(SerginUiModuleCatalog).Assembly];
        NavSections =
        [
            .. modules
                .GroupBy(module => module.NavGroup)
                .OrderBy(group => group.Key.Order)
                .ThenBy(group => group.Key.Title.Default, StringComparer.Ordinal)
                .Select(group => new SerginNavSection(group.Key, [.. InDrawerOrder(group)]))
        ];
        NavItems = [.. NavSections.SelectMany(section => section.Items)];
    }

    public IReadOnlyCollection<ISerginWebUiModule> Modules { get; }

    /// <summary>
    /// Every assembly the router must scan for <c>@page</c> templates: each module's UI assembly, plus the
    /// shell's own, which carries <c>SerginHomePage</c>.
    /// </summary>
    /// <remarks>
    /// The shell assembly belongs here rather than only in the host bootstrap's
    /// <c>AddAdditionalAssemblies(...)</c> call because it has two consumers, and missing either one
    /// breaks half the routing: the bootstrap maps the server-side endpoint, while a host's
    /// <c>Routes.razor</c> feeds the same list to <c>Router.AdditionalAssemblies</c> for in-app
    /// navigation. Adding it here reaches both without any host having to change.
    ///
    /// It does not widen the route-prefix guard, which walks <see cref="Modules"/> — so the shell's own
    /// "/" is exempt from the <c>/{schema}/</c> rule that module pages must follow, which is the point.
    /// </remarks>
    public IReadOnlyCollection<Assembly> RoutableAssemblies { get; }

    /// <summary>
    /// One section per group (modules returning the same group share a section), groups ordered by <see cref="SerginNavGroup.Order"/> then title.
    /// Inside a group, one subsection per module under its <see cref="ISerginWebUiModule.NavCaption"/>, ordered by the module's lowest
    /// <see cref="SerginNavItem.Order"/> then caption, each subsection's entries by <see cref="SerginNavItem.Order"/> then label (ordinal throughout).
    /// A module with no entries gets no subsection.
    /// </summary>
    public IReadOnlyCollection<SerginNavSection> NavSections { get; }

    /// <summary>
    /// Every module's entries, flattened in drawer order.
    /// </summary>
    public IReadOnlyCollection<SerginNavItem> NavItems { get; }

    /// <summary>
    /// The group of the module whose entry has exactly this href, or <see langword="null"/> when no module entry
    /// has it. The breadcrumb strip uses it to name the module of a trail's section step.
    /// </summary>
    public SerginNavGroup? GroupOf(string href)
        => NavSections.FirstOrDefault(section => section.Items.Any(item => item.Href == href))?.Group;

    private static IEnumerable<SerginNavSubsection> InDrawerOrder(IEnumerable<ISerginWebUiModule> modules)
        => modules
            .Where(module => module.NavItems.Count > 0)
            .Select(module => new SerginNavSubsection(module.NavCaption, [.. InDrawerOrder(module.NavItems)]))
            .OrderBy(subsection => subsection.Items.First().Order)
            .ThenBy(subsection => subsection.Caption?.Default, StringComparer.Ordinal);

    private static IEnumerable<SerginNavItem> InDrawerOrder(IEnumerable<SerginNavItem> items)
        => items.OrderBy(item => item.Order).ThenBy(item => item.Label.Default, StringComparer.Ordinal);
}
