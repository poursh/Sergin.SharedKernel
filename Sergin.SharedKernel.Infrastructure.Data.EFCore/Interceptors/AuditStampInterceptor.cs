using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Interceptors;

/// <summary>
/// Stamps the audit shadow properties (<see cref="AuditColumns"/>) on every entity type whose model carries
/// them. Added: created_*, with modified_* left NULL. Modified: modified_*, with created_* marked
/// unmodified so nothing can overwrite it. Runs after EventDispatcherInterceptor, so entities a domain-event
/// handler adds or changes on the same save are stamped too. The actor is the scope's IUserContext: the
/// signed-in user through the Blazor dispatcher, the relay identity during outbox delivery.
/// <para>
/// Row-level only: a root is not stamped when only its child entity changed, and raw-SQL writes bypass this.
/// The synchronous path stamps the same way — unlike event dispatch there is nothing asynchronous to refuse.
/// </para>
/// </summary>
internal sealed class AuditStampInterceptor(IUserContext user, IDateTimeProvider clock) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Stamp(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Stamp(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTime now = clock.UtcNow;
        Guid actor = user.Id.Value;

        // Entries() runs DetectChanges, so a property changed since the last detection is already Modified.
        foreach (EntityEntry entry in context.ChangeTracker.Entries())
        {
            if (!AuditColumns.IsAudited(entry.Metadata))
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(AuditColumns.CreatedAtUtc).CurrentValue = now;
                    entry.Property(AuditColumns.CreatedBy).CurrentValue = actor;
                    break;

                case EntityState.Modified:
                    entry.Property(AuditColumns.ModifiedAtUtc).CurrentValue = now;
                    entry.Property(AuditColumns.ModifiedBy).CurrentValue = actor;
                    entry.Property(AuditColumns.CreatedAtUtc).IsModified = false;
                    entry.Property(AuditColumns.CreatedBy).IsModified = false;
                    break;

                default:
                    break;
            }
        }
    }
}
