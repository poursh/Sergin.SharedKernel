namespace Sergin.SharedKernel.Modules;

/// <summary>
/// The drawer group a UI module's entries are shown under, and the module step of a breadcrumb trail.
/// </summary>
/// <param name="Title">Display text, usually the module's name. Rendered through ILocalizer, so a resource key also works.</param>
/// <param name="Icon">Raw SVG path data. A string keeps this contract leaf free of any UI library.</param>
/// <param name="Order">Ordering among the groups and the home entry; ties broken by Title.</param>
public sealed record SerginNavGroup(string Title, string Icon, int Order = 0);
