using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;

/// <summary>
/// Finds the versioned aggregate roots a pending save changes: a root entry that is itself changed, and the
/// root of every changed child entry, found by walking the child's foreign keys up the aggregate's own
/// navigations (the same principal-to-dependent navigations the convention walked down) to a tracked entry.
/// A changed child whose root is not tracked is refused: every write goes through the root's behaviour, so the
/// root is always loaded.
/// </summary>
internal static class VersionedRoots
{
    public static IReadOnlyList<EntityEntry> Touched(DbContext context, bool includeAdded)
    {
        EntityEntry[] tracked = [.. context.ChangeTracker.Entries()];
        Dictionary<object, EntityEntry> roots = new(ReferenceEqualityComparer.Instance);

        foreach (EntityEntry entry in tracked.Where(IsChanged))
        {
            EntityEntry? root = VersionedRootOf(tracked, entry);

            if (root is not null && (includeAdded || root.State != EntityState.Added))
            {
                roots.TryAdd(root.Entity, root);
            }
        }

        return [.. roots.Values];
    }

    private static EntityEntry? VersionedRootOf(EntityEntry[] tracked, EntityEntry entry)
    {
        if (RowVersionColumns.IsVersioned(entry.Metadata))
        {
            return entry;
        }

        if (RowVersionColumns.RootOf(entry.Metadata) is not { } rootName)
        {
            return null;
        }

        return RootOf(tracked, entry, rootName)
            ?? throw new InvalidOperationException(
                $"{entry.Metadata.DisplayName()} changed, but its aggregate root {rootName} is not tracked. "
                + "Load the root and change its child through it, so the root's version moves with the change.");
    }

    private static bool IsChanged(EntityEntry entry) =>
        entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;

    private static EntityEntry? RootOf(EntityEntry[] tracked, EntityEntry child, string rootName)
    {
        foreach (IForeignKey foreignKey in child.Metadata.GetForeignKeys())
        {
            IEntityType principalType = foreignKey.PrincipalEntityType;

            bool alongTheAggregate = (foreignKey.PrincipalToDependent is not null || foreignKey.IsOwnership)
                && (RowVersionColumns.IsVersioned(principalType)
                    ? IsOrDerivesFrom(principalType, rootName)
                    : RowVersionColumns.RootOf(principalType) == rootName);

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

            return RowVersionColumns.IsVersioned(principal.Metadata) ? principal : RootOf(tracked, principal, rootName);
        }

        return null;
    }

    private static bool IsOrDerivesFrom(IReadOnlyEntityType entityType, string name)
    {
        for (IReadOnlyEntityType? current = entityType; current is not null; current = current.BaseType)
        {
            if (current.Name == name)
            {
                return true;
            }
        }

        return false;
    }
}
