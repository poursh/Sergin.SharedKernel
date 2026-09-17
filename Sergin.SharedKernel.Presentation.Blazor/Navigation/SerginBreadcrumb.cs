using Sergin.SharedKernel.Modules;

namespace Sergin.SharedKernel.Presentation.Blazor.Navigation;

/// <summary>
/// One step of a page's breadcrumb trail, declared by the page and rendered by <see cref="SerginBreadcrumbs"/>.
/// </summary>
/// <param name="Label">
/// Display text. Rendered through <c>ILocalizer</c>, so a resource key also works — and an entity name passes
/// through untouched, since the default localizer returns an unknown key as is.
/// </param>
/// <param name="Href">
/// Absolute, schema-prefixed path, or <see langword="null"/> for a step that is not a link. The last step of a
/// trail is the current page and is never a link, whatever this says; <see cref="SerginBreadcrumbs"/> enforces
/// that so no page has to.
/// </param>
public sealed record SerginBreadcrumb(string Label, string? Href = null)
{
    /// <summary>
    /// The step for a module section, taken from the same nav entry the drawer renders so the two cannot drift.
    /// </summary>
    public static SerginBreadcrumb Of(SerginNavItem item) => new(item.Label, item.Href);
}
