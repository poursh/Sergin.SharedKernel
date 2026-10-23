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
                .OrderBy(module => module.NavGroup.Order)
                .ThenBy(module => module.NavGroup.Title, StringComparer.Ordinal)
                .Select(module => new SerginNavSection(module.NavGroup, [.. InDrawerOrder(module.NavItems)]))
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
    /// One section per module, groups ordered by <see cref="SerginNavGroup.Order"/> then title, each group's
    /// entries by <see cref="SerginNavItem.Order"/> then label (ordinal both times).
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

    private static IEnumerable<SerginNavItem> InDrawerOrder(IEnumerable<SerginNavItem> items)
        => items.OrderBy(item => item.Order).ThenBy(item => item.Label, StringComparer.Ordinal);
}
