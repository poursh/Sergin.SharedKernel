using System.Reflection;

namespace Sergin.SharedKernel.Modules;

public interface ISerginWebUiModule : ISerginModule
{
    /// <summary>
    /// The assembly holding this module's routable Razor components. Needed by both
    /// MapRazorComponents&lt;T&gt;().AddAdditionalAssemblies(...) for static server-side rendering and
    /// the Router component's AdditionalAssemblies for interactive routing. This is never the
    /// ApplicationAssembly, which is deliberately UI-free.
    /// </summary>
    Assembly UiAssembly { get; }

    IReadOnlyCollection<SerginNavItem> NavItems { get; }
}
