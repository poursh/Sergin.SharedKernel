namespace Sergin.SharedKernel.Application.Securities.Authorization;

/// <summary>
/// One permission in the catalogue and the aggregate root that registered it; <see cref="AggregateRoot"/> is
/// null only for <see cref="PermissionDefinition.AllPlatform"/>, which belongs to the platform.
/// </summary>
public sealed record PermissionCatalogEntry(PermissionDefinition Definition, Type? AggregateRoot);

/// <summary>One module's permissions in the catalogue, grouped by aggregate.</summary>
public sealed record PermissionCatalogModule(string Schema, IReadOnlyList<PermissionCatalogGroup> Groups);

/// <summary>One aggregate's permissions: the root's type name, or <c>Platform</c> for the platform permission.</summary>
public sealed record PermissionCatalogGroup(string Name, IReadOnlyList<PermissionDefinition> Permissions);
