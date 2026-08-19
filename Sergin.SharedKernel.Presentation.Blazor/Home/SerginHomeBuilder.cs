using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Modules;

namespace Sergin.SharedKernel.Presentation.Blazor.Home;

/// <summary>
/// Configures the home slot. Handed to <c>AddSerginBlazorApp</c>'s <c>configureHome</c> callback; a host that
/// passes no callback gets <see cref="SerginWelcome"/> under a "Home" nav entry.
/// </summary>
public sealed class SerginHomeBuilder
{
    private Type componentType = typeof(SerginWelcome);

    private SerginNavItem? navItem = NavItemFor("Home", Icons.Material.Filled.Home, order: 0);

    /// <summary>
    /// Renders <typeparamref name="TComponent"/> at the site root.
    /// </summary>
    /// <remarks>
    /// The <c>new()</c> half of the constraint is load-bearing, not ceremony. Blazor does not inject into
    /// component constructors — the renderer's component factory instantiates through
    /// <c>Activator.CreateInstance</c> — so a component written with a primary constructor would throw on
    /// the first render of the root page. The constraint turns that runtime failure into a compile error,
    /// which is the whole reason this is a generic method rather than a <c>Type</c> property validated at
    /// startup.
    /// </remarks>
    public SerginHomeBuilder UseComponent<TComponent>()
        where TComponent : IComponent, new()
    {
        componentType = typeof(TComponent);

        return this;
    }

    /// <summary>
    /// Relabels the shell's nav entry for the root.
    /// </summary>
    /// <remarks>
    /// There is deliberately no href parameter. The root's path is fixed by
    /// <see cref="SerginHomePage"/>'s <c>@page</c> template, so an href supplied here could only either
    /// repeat it or contradict it — and a contradiction is a nav entry that 404s, with nothing at
    /// startup to catch it. Label, icon and order are the host's to choose; the path is not.
    /// </remarks>
    public SerginHomeBuilder UseNavItem(string label, string icon, int order = 0)
    {
        navItem = NavItemFor(label, icon, order);

        return this;
    }

    /// <summary>
    /// Drops the shell's nav entry for the root, leaving the home page reachable only by URL.
    /// </summary>
    public SerginHomeBuilder WithoutNavItem()
    {
        navItem = null;

        return this;
    }

    /// <summary>
    /// Takes the immutable snapshot the container holds. Public because the host bootstrap that calls it
    /// (<c>Sergin.SharedKernel.Hosts.WebUi</c>) is a separate assembly, and an <c>InternalsVisibleTo</c>
    /// for one method costs more than the encapsulation it buys.
    /// </summary>
    public SerginHome Build() => new(componentType, navItem);

    private static SerginNavItem NavItemFor(string label, string icon, int order)
        => new(label, SerginHome.RootPath, icon, order);
}
