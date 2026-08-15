namespace Sergin.SharedKernel.Modules;

/// <param name="Label">Display text. Rendered through ILocalizer, so a resource key also works.</param>
/// <param name="Href">Absolute, schema-prefixed path, e.g. "/mm/devices".</param>
/// <param name="Icon">Raw SVG path data. A string keeps this contract leaf free of any UI library.</param>
/// <param name="Order">Cross-module ordering; ties broken by Label.</param>
public sealed record SerginNavItem(string Label, string Href, string Icon, int Order = 0);
