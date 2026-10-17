using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Domain.Securities;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Interceptors;

/// <summary>
/// Checks every permission the request required (<see cref="RequiredPermissionsContext"/>) again on the scope of each
/// record the save writes, so a request allowed at one scope cannot change a record that lives in another. A scoped
/// root is checked on the scope it is stored in; a child, on its root's. A record of an aggregate that is not scoped
/// is global data and lives at the root, so it is checked there: a permission held at a region does not reach the
/// catalog. A new scoped root is skipped, since it is stamped with the current scope the pipeline already checked.
/// A denial throws <see cref="ForbiddenException"/>, which the permission behavior answers with
/// <c>Error.Forbidden()</c>; nothing is saved.
/// <para>
/// With no permissions published there is nothing to check: an unconfigured request, an outbox consumer, system
/// code. Runs after EventDispatcherInterceptor, so what a domain-event handler changes is checked too, and before
/// SoftDeleteInterceptor, while a delete is still a delete. The check is asynchronous, so the synchronous SaveChanges
/// refuses a save that has something to check.
/// </para>
/// </summary>
internal sealed class RecordScopeInterceptor(RequiredPermissionsContext required, IScopeAuthorization authorization) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is { } context && required.Permissions.Count > 0)
        {
            foreach (Guid scopeId in TargetScopes(context))
            {
                foreach (Permission permission in required.Permissions)
                {
                    if (!await authorization.CurrentUserHasAsync(permission, scopeId, cancellationToken).ConfigureAwait(false))
                    {
                        throw new ForbiddenException();
                    }
                }
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is { } context && required.Permissions.Count > 0 && TargetScopes(context).Count > 0)
        {
            throw new InvalidOperationException(
                "This save writes records the request's permissions must be checked on, and that check is asynchronous. "
                + "Save with SaveChangesAsync.");
        }

        return base.SavingChanges(eventData, result);
    }

    private static HashSet<Guid> TargetScopes(DbContext context)
    {
        EntityEntry[] tracked = [.. context.ChangeTracker.Entries()];
        HashSet<Guid> scopes = [];

        foreach (EntityEntry entry in tracked.Where(IsChangedRecord))
        {
            if (TargetScope(tracked, entry) is { } scopeId)
            {
                scopes.Add(scopeId);
            }
        }

        return scopes;
    }

    private static bool IsChangedRecord(EntityEntry entry) =>
        entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && entry.Entity is IEntity;

    private static Guid? TargetScope(EntityEntry[] tracked, EntityEntry entry)
    {
        EntityEntry? scopedRoot;

        if (ScopeColumns.IsScoped(entry.Metadata))
        {
            scopedRoot = entry;
        }
        else if (ScopeColumns.RootOf(entry.Metadata) is { } rootName)
        {
            scopedRoot = RootOf(tracked, entry, rootName)
                ?? throw new InvalidOperationException(
                    $"{entry.Metadata.DisplayName()} changed, but its scoped aggregate root {rootName} is not tracked. "
                    + "Load the root and change its child through it, so the change is checked on the root's scope.");
        }
        else
        {
            return AuthorizationScope.RootId;
        }

        // A new root takes the current scope, which the pipeline checked before the handler ran.
        return scopedRoot.State == EntityState.Added
            ? null
            : (Guid)scopedRoot.Property(ScopeColumns.ScopeId).OriginalValue!;
    }

    /// <summary>The tracked root of <paramref name="child"/>, walked up the aggregate's own navigations, as VersionedRoots does.</summary>
    private static EntityEntry? RootOf(EntityEntry[] tracked, EntityEntry child, string rootName)
    {
        foreach (IForeignKey foreignKey in child.Metadata.GetForeignKeys())
        {
            IEntityType principalType = foreignKey.PrincipalEntityType;

            bool alongTheAggregate = (foreignKey.PrincipalToDependent is not null || foreignKey.IsOwnership)
                && (ScopeColumns.IsScoped(principalType) || ScopeColumns.RootOf(principalType) == rootName);

            if (!alongTheAggregate)
            {
                continue;
            }

            object?[] key = [.. foreignKey.Properties.Select(property => child.Property(property.Name).CurrentValue)];

            EntityEntry? principal = tracked.FirstOrDefault(entry =>
                principalType.IsAssignableFrom(entry.Metadata)
                && foreignKey.PrincipalKey.Properties
                    .Select(property => entry.Property(property.Name).CurrentValue)
                    .SequenceEqual(key));

            if (principal is null)
            {
                return null;
            }

            return ScopeColumns.IsScoped(principal.Metadata) ? principal : RootOf(tracked, principal, rootName);
        }

        return null;
    }
}
