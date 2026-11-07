using System.Reflection;
using Sergin.SharedKernel.Application.Localizations;

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

    /// <summary>
    /// The drawer group this module's <see cref="NavItems"/> are shown under. It also names the module in every
    /// breadcrumb trail whose first step is one of them.
    /// </summary>
    SerginNavGroup NavGroup { get; }

    /// <summary>
    /// The caption this module's <see cref="NavItems"/> sit under inside a <see cref="NavGroup"/> that several
    /// modules share, or <see langword="null"/> for none. The drawer shows captions only when a group holds entries
    /// from two or more modules, so a module with a group of its own needs none.
    /// </summary>
    TextDefinition? NavCaption => null;

    IReadOnlyCollection<SerginNavItem> NavItems { get; }
}
