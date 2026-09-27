using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Interceptors;

/// <summary>
/// Turns the delete of a soft-deletable entity (<see cref="SoftDeleteColumns"/>) into an UPDATE of its
/// deleted_at_utc/deleted_by. A repository's <c>Remove</c> and a child dropped from its root's collection
/// both arrive here as <see cref="EntityState.Deleted"/>; other types keep their real delete.
/// <para>
/// The delete cascades through the aggregate: every child reached from the deleted entity by a
/// principal-to-dependent navigation, stopping at another aggregate root, is stamped with the same instant
/// and actor. A child not yet loaded is loaded first, because no DELETE reaches the database to fire its
/// cascade. Owned entries EF marked deleted along with their owner are put back to unchanged: the owner's
/// row survives, and so do they.
/// </para>
/// <para>
/// Runs after AuditStampInterceptor, which skips deleted entries, so a soft delete stamps deleted_* and
/// leaves modified_* as the last edit left it. The actor is the scope's IUserContext, as for audit.
/// Raw-SQL deletes bypass this.
/// </para>
/// </summary>
internal sealed class SoftDeleteInterceptor(IUserContext user, IDateTimeProvider clock) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is { } context)
        {
            Stamp stamp = new(clock.UtcNow, user.Id.Value);

            foreach (EntityEntry entry in PendingDeletes(context))
            {
                await SoftDeleteAsync(entry, stamp, cancellationToken).ConfigureAwait(false);
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is { } context)
        {
            Stamp stamp = new(clock.UtcNow, user.Id.Value);

            foreach (EntityEntry entry in PendingDeletes(context))
            {
                SoftDelete(entry, stamp);
            }
        }

        return base.SavingChanges(eventData, result);
    }

    // Materialized: stamping changes entry states while this list is walked. A child reached by a cascade is
    // stamped then and is no longer Deleted by the time the list reaches it, so it is skipped there.
    private static IReadOnlyCollection<EntityEntry> PendingDeletes(DbContext context) =>
        [.. context.ChangeTracker.Entries().Where(IsPendingSoftDelete)];

    private static bool IsPendingSoftDelete(EntityEntry entry) =>
        entry.State == EntityState.Deleted && SoftDeleteColumns.IsSoftDeletable(entry.Metadata);

    private static async Task SoftDeleteAsync(EntityEntry entry, Stamp stamp, CancellationToken cancellationToken)
    {
        if (!Keep(entry, stamp))
        {
            return;
        }

        foreach (NavigationEntry navigation in AggregateNavigations(entry))
        {
            if (!navigation.IsLoaded)
            {
                await navigation.LoadAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (EntityEntry child in Targets(entry.Context, navigation))
            {
                await SoftDeleteAsync(child, stamp, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static void SoftDelete(EntityEntry entry, Stamp stamp)
    {
        if (!Keep(entry, stamp))
        {
            return;
        }

        foreach (NavigationEntry navigation in AggregateNavigations(entry))
        {
            if (!navigation.IsLoaded)
            {
                navigation.Load();
            }

            foreach (EntityEntry child in Targets(entry.Context, navigation))
            {
                SoftDelete(child, stamp);
            }
        }
    }

    /// <summary>
    /// Keeps <paramref name="entry"/>'s row: a soft-deletable entry not yet stamped gets the stamp, and an owned
    /// entry goes back to unchanged if EF cascaded the delete to it. Answers whether to walk on below it.
    /// </summary>
    private static bool Keep(EntityEntry entry, Stamp stamp)
    {
        if (SoftDeleteColumns.IsSoftDeletable(entry.Metadata))
        {
            PropertyEntry deletedAt = entry.Property(SoftDeleteColumns.DeletedAtUtc);

            // Already stamped, by this pass or an earlier save: nothing to do below it either.
            if (entry.State != EntityState.Deleted && deletedAt.CurrentValue is not null)
            {
                return false;
            }

            entry.State = EntityState.Unchanged;
            deletedAt.CurrentValue = stamp.At;
            entry.Property(SoftDeleteColumns.DeletedBy).CurrentValue = stamp.By;
            return true;
        }

        if (entry.Metadata.FindOwnership() is not null)
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Unchanged;
            }

            return true;
        }

        return false;
    }

    private static IEnumerable<NavigationEntry> AggregateNavigations(EntityEntry entry) =>
        entry.Navigations.Where(navigation =>
            navigation.Metadata is INavigation { IsOnDependent: false } metadata
            && !typeof(IAggregateRoot).IsAssignableFrom(metadata.TargetEntityType.ClrType));

    private static IEnumerable<EntityEntry> Targets(DbContext context, NavigationEntry navigation) =>
        navigation switch
        {
            CollectionEntry collection => collection.CurrentValue?.Cast<object>().Select(context.Entry) ?? [],
            ReferenceEntry { CurrentValue: { } target } => [context.Entry(target)],
            _ => [],
        };

    private readonly record struct Stamp(DateTime At, Guid By);
}
