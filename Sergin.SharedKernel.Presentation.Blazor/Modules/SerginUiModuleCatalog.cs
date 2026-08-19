using System.Reflection;
using Sergin.SharedKernel.Modules;

namespace Sergin.SharedKernel.Presentation.Blazor.Modules;

public sealed class SerginUiModuleCatalog
{
    public SerginUiModuleCatalog(IReadOnlyCollection<ISerginWebUiModule> modules)
    {
        Modules = modules;
        RoutableAssemblies = [.. modules.Select(module => module.UiAssembly), typeof(SerginUiModuleCatalog).Assembly];
        NavItems =
        [
            .. modules.SelectMany(module => module.NavItems)
                .OrderBy(item => item.Order)
                .ThenBy(item => item.Label, StringComparer.Ordinal)
        ];
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

    public IReadOnlyCollection<SerginNavItem> NavItems { get; }
}
