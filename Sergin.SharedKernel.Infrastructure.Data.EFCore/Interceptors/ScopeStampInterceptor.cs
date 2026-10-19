using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Interceptors;

/// <summary>
/// Stamps a new scoped root (<see cref="ScopeColumns"/>) with the scope the caller works in: scope_id from
/// <see cref="IAuthorizationScopeContext"/> and scope_path from <see cref="IScopePathResolver"/>. A scope the resolver
/// does not know is refused with <see cref="ForbiddenException"/> and nothing is saved. An update never moves a
/// record: both columns are left out of every UPDATE. Only a scope move rewrites the path, through
/// <c>ScopePathRewriteHandler</c>.
/// <para>
/// Runs after EventDispatcherInterceptor, so a root a domain-event handler adds is stamped too. The path lookup is
/// asynchronous, so the synchronous SaveChanges refuses a save that adds a scoped root.
/// </para>
/// </summary>
internal sealed class ScopeStampInterceptor(IAuthorizationScopeContext scopeContext, IScopePathResolver paths) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is { } context)
        {
            foreach (EntityEntry entry in ScopedEntries(context))
            {
                if (entry.State == EntityState.Added)
                {
                    await StampAsync(entry, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    KeepScope(entry);
                }
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is { } context)
        {
            foreach (EntityEntry entry in ScopedEntries(context))
            {
                if (entry.State == EntityState.Added)
                {
                    throw new InvalidOperationException(
                        $"A new {entry.Metadata.DisplayName()} is scoped, and its scope path is looked up asynchronously. "
                        + "Save it with SaveChangesAsync.");
                }

                KeepScope(entry);
            }
        }

        return base.SavingChanges(eventData, result);
    }

    private static IReadOnlyCollection<EntityEntry> ScopedEntries(DbContext context) =>
    [
        .. context.ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified && ScopeColumns.IsScoped(entry.Metadata)),
    ];

    private async Task StampAsync(EntityEntry entry, CancellationToken cancellationToken)
    {
        Guid scopeId = scopeContext.ScopeId;

        string path = await paths.GetPathAsync(scopeId, cancellationToken).ConfigureAwait(false)
            ?? throw new ForbiddenException();

        entry.Property(ScopeColumns.ScopeId).CurrentValue = scopeId;
        entry.Property(ScopeColumns.ScopePath).CurrentValue = new LTree(path);
    }

    private static void KeepScope(EntityEntry entry)
    {
        entry.Property(ScopeColumns.ScopeId).IsModified = false;
        entry.Property(ScopeColumns.ScopePath).IsModified = false;
    }
}
