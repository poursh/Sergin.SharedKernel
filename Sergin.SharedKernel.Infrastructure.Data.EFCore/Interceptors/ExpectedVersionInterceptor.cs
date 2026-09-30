using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Interceptors;

/// <summary>
/// Puts <see cref="ConcurrencyContext.Expected"/> into the save as the original value of the changed root's
/// row_version, so the UPDATE (or soft-delete UPDATE, or DELETE) matches only a row still at that version, and
/// a mismatch is EF's DbUpdateConcurrencyException. Registered first, before EventDispatcherInterceptor, so it
/// sees only what the handler changed: a root a domain-event handler changes later is bumped by
/// RowVersionBumpInterceptor but not checked. An expected version guards exactly one root; a save changing
/// more than one with a version set is a programming error.
/// </summary>
internal sealed class ExpectedVersionInterceptor(ConcurrencyContext concurrency) : SaveChangesInterceptor
{
    /// <summary>
    /// The entity whose version the current save checks, for RowVersionBumpInterceptor, which publishes that
    /// root's new version.
    /// </summary>
    public object? CheckedRoot { get; private set; }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Apply(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Apply(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    private void Apply(DbContext? context)
    {
        CheckedRoot = null;

        if (context is null || concurrency.Expected is not { } expected)
        {
            return;
        }

        IReadOnlyList<EntityEntry> roots = VersionedRoots.Touched(context, includeAdded: false);

        if (roots.Count > 1)
        {
            throw new InvalidOperationException(
                $"An expected version guards one aggregate, but this save changes {roots.Count} versioned roots: "
                + $"{string.Join(", ", roots.Select(root => root.Metadata.DisplayName()))}. "
                + "Change one aggregate per command, or send no expected version.");
        }

        if (roots.Count == 1)
        {
            roots[0].Property(RowVersionColumns.RowVersion).OriginalValue = expected.Value;
            CheckedRoot = roots[0].Entity;
        }
    }
}
