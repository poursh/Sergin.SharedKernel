namespace Sergin.SharedKernel.Modules;

/// <param name="Label">Display text. Rendered through ILocalizer, so a resource key also works.</param>
/// <param name="Href">Absolute, schema-prefixed path, e.g. "/mm/devices".</param>
/// <param name="Icon">Raw SVG path data. A string keeps this contract leaf free of any UI library.</param>
/// <param name="Order">Cross-module ordering; ties broken by Label.</param>
/// <param name="RequiredPermission">
/// Permission the current user must hold for this entry to appear, or null to always show it. A plain
/// string, like <paramref name="Icon"/>, so this contract leaf stays free of any dependency — the nav
/// menu parses it. Hiding the entry is presentation only; the page behind it is still gated by the
/// handler's own <c>[RequiredPermissions]</c>.
/// </param>
public sealed record SerginNavItem(
    string Label, string Href, string Icon, int Order = 0, string? RequiredPermission = null);
