using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Application.Securities.Authorization;

/// <summary>
/// One permission in the catalogue, the aggregate root that registered it, and the declared text its label is shown
/// with; <see cref="AggregateRoot"/> is null only for <see cref="PermissionDefinition.AllPlatform"/>, which belongs to
/// the platform.
/// </summary>
/// <param name="Label">
/// <c>&lt;root's text category&gt;.permissions.&lt;last segment of the name&gt;</c>, its default the definition's
/// label. See <see cref="PermissionCatalog"/> for the fallbacks.
/// </param>
/// <param name="GroupTitle">
/// The heading the entry's aggregate group is shown under: the name the root declared with <c>Named(...)</c>,
/// <see cref="Localizations.Common.CommonTexts.PermissionGroupPlatform"/> for the platform permission, else
/// <see cref="Localizations.Common.CommonTexts.PermissionGroupOther"/>.
/// </param>
public sealed record PermissionCatalogEntry(PermissionDefinition Definition, Type? AggregateRoot, TextDefinition Label, TextDefinition GroupTitle)
{
    /// <summary>Whether <see cref="Label"/> belongs to a module's keys and so is in the text catalog.</summary>
    internal bool InTextCatalog { get; init; } = true;
}

/// <summary>One module's permissions in the catalogue, grouped by aggregate.</summary>
public sealed record PermissionCatalogModule(string Schema, IReadOnlyList<PermissionCatalogGroup> Groups);

/// <summary>
/// One aggregate's permissions. <paramref name="Name"/> is the root's type name, or <c>Platform</c> for the platform
/// permission — an ordering key, never shown; <paramref name="Title"/> is the declared heading the pickers show.
/// </summary>
public sealed record PermissionCatalogGroup(string Name, TextDefinition Title, IReadOnlyList<PermissionDefinition> Permissions);
