using Sergin.SharedKernel.Domain.Securities;

namespace Sergin.SharedKernel.Modules;

/// <param name="Label">Display text. Rendered through ILocalizer, so a resource key also works.</param>
/// <param name="Href">Absolute, schema-prefixed path, e.g. "/mm/devices".</param>
/// <param name="Icon">Raw SVG path data. A string keeps this contract leaf free of any UI library.</param>
/// <param name="Order">Cross-module ordering; ties broken by Label.</param>
/// <param name="RequiredPermission">
/// Permission the current user must hold for this entry to appear, or null to always show it: a definition an
/// aggregate registered with <c>Permissions(...)</c>, which the UI host checks at start, refusing to start over
/// one the permission catalogue does not hold. Hiding the entry is presentation only; the page behind it is still
/// gated by the request's own required permission (its command configuration). Shown when the user holds it on any scope, so
/// the entry stays reachable whichever scope the user works in now; the page answers for the current scope.
/// </param>
public sealed record SerginNavItem(
    string Label, string Href, string Icon, int Order = 0, PermissionDefinition? RequiredPermission = null);
