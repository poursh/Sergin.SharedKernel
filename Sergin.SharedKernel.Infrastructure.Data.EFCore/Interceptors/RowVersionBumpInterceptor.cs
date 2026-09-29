using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Interceptors;

/// <summary>
/// Writes a new row_version to every versioned root the save changes, whoever changed it: the handler, a
/// domain-event handler, the soft-delete cascade. Registered last, after SoftDeleteInterceptor, so it sees all
/// of them. A root left unchanged while its child changed has only its row_version marked modified: that is
/// what makes one version cover the whole aggregate. A hard-deleted root is left alone; its DELETE is checked
/// against the original value. After a successful save, the version is published to
/// <see cref="ConcurrencyContext.Current"/>: the checked root's (named by ExpectedVersionInterceptor, the same
/// scoped instance), or the only root's when no version was sent (a create). When a version was sent but its
/// aggregate was not written (the command changed nothing, or only a domain-event handler changed another
/// root), the version published is the one sent: publishing another root's would hand the caller a version
/// that belongs to a different aggregate.
/// </summary>
internal sealed class RowVersionBumpInterceptor(ConcurrencyContext concurrency, ExpectedVersionInterceptor check)
    : SaveChangesInterceptor
{
    private EntityEntry? toPublish;
    private bool publishExpected;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Bump(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Bump(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Publish();
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Publish();
        return base.SavedChanges(eventData, result);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        Clear();
        base.SaveChangesFailed(eventData);
    }

    private void Bump(DbContext? context)
    {
        Clear();

        if (context is null)
        {
            return;
        }

        IReadOnlyList<EntityEntry> roots =
            [.. VersionedRoots.Touched(context, includeAdded: true).Where(root => root.State != EntityState.Deleted)];

        EntityEntry? checkedRoot = roots.FirstOrDefault(root => ReferenceEquals(root.Entity, check.CheckedRoot));

        // Re-applied, not trusted: SoftDeleteInterceptor sets a deleted root back to Unchanged before stamping
        // it, and a state change can reset original values to the current ones, which would drop the check.
        if (checkedRoot is not null && concurrency.Expected is { } expected)
        {
            checkedRoot.Property(RowVersionColumns.RowVersion).OriginalValue = expected.Value;
        }

        foreach (EntityEntry root in roots)
        {
            PropertyEntry version = root.Property(RowVersionColumns.RowVersion);
            version.CurrentValue = RowVersion.Create().Value;

            if (root.State == EntityState.Unchanged)
            {
                version.IsModified = true;
            }
        }

        if (checkedRoot is not null)
        {
            toPublish = checkedRoot;
        }
        else if (concurrency.Expected is not null)
        {
            publishExpected = true;
        }
        else if (roots.Count == 1)
        {
            toPublish = roots[0];
        }
    }

    private void Publish()
    {
        if (toPublish is not null)
        {
            concurrency.Current = RowVersion.Create((Guid)toPublish.Property(RowVersionColumns.RowVersion).CurrentValue!);
        }
        else if (publishExpected)
        {
            concurrency.Current = concurrency.Expected;
        }

        Clear();
    }

    private void Clear()
    {
        toPublish = null;
        publishExpected = false;
    }
}
