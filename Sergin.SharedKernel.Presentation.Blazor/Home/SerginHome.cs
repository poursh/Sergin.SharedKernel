using Sergin.SharedKernel.Modules;

namespace Sergin.SharedKernel.Presentation.Blazor.Home;

/// <summary>
/// What the shell renders at the site root, and how the root is labelled in the nav menu.
/// </summary>
/// <remarks>
/// Registered as a singleton and injected as itself, mirroring <c>SerginUiModuleCatalog</c>. It is
/// deliberately <em>not</em> an <c>IOptions&lt;T&gt;</c>: unlike <c>DevUserOptions</c> or
/// <c>SerginApplicationOptions</c> this is composed in code through <c>AddSerginWebUi</c>'s
/// <c>configureHome</c> callback and is bound to no <c>appsettings.json</c> key, so none of the
/// options machinery — section binding, named options, reload, validation — would carry anything.
/// </remarks>
public sealed class SerginHome
{
    /// <summary>
    /// The route <see cref="SerginHomePage"/> claims, and therefore the only href the home nav entry can
    /// carry. A host chooses the home <em>component</em>, never the home <em>path</em> — which is why
    /// <see cref="SerginHomeBuilder.UseNavItem"/> takes a label and icon but no href.
    /// </summary>
    /// <remarks>
    /// Razor <c>@page</c> templates must be literals, so <c>SerginHomePage.razor</c> spells "/" out
    /// again rather than referencing this. Change one and you must change the other.
    /// </remarks>
    public const string RootPath = "/";

    public SerginHome(Type componentType, SerginNavItem? navItem)
    {
        ComponentType = componentType;
        NavItem = navItem;
    }

    /// <summary>
    /// The component the root page renders. Guaranteed by <see cref="SerginHomeBuilder"/> to be an
    /// <c>IComponent</c> with a public parameterless constructor.
    /// </summary>
    public Type ComponentType { get; }

    /// <summary>
    /// The shell's own nav entry for the root, always pointing at <see cref="RootPath"/>.
    /// <see langword="null"/> hides it, for a host that wants a home page reachable only by URL.
    /// </summary>
    public SerginNavItem? NavItem { get; }
}
