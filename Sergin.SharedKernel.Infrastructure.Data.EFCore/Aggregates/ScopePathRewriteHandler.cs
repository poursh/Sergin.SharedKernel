using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Sergin.SharedKernel.Application.Events.Integration;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Domain.ScheduledTasks;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.ScheduledTasks;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;

/// <summary>
/// Rewrites the stored scope_path of every record of <typeparamref name="TContext"/>'s scoped aggregates in the scopes
/// a move re-pathed. Each scope's path is looked up as it is now, never taken from the event, so the handler needs no
/// inbox: delivering an event twice, or two moves out of order, ends with the current paths. A scope that no longer
/// exists is skipped. One UPDATE per scope and scoped table, run straight against the database: no interceptor sees
/// it, so it bumps no row version and stamps no modification, since the path is derived from the scope tree, not
/// data anyone edited.
/// <para>
/// Registered for every module context by <c>AddModuleDbContext</c>; a context with no scoped aggregate does nothing.
/// A context opted into scheduled tasks also has its <c>scheduled_task_runs</c> re-pathed: a run's scope path is a
/// plain column the run reads are gated on, so it would otherwise keep the old path.
/// </para>
/// </summary>
internal sealed class ScopePathRewriteHandler<TContext>(TContext context, IScopePathResolver paths)
    : IIntegrationEventHandler<ScopesMovedIntegrationEvent>
    where TContext : SerginDbContext
{
    public async Task Handle(ScopesMovedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        string[] tables =
        [
            .. context.Model.GetEntityTypes()
                .Where(entityType => entityType.BaseType is null && ScopeColumns.IsScoped(entityType))
                .Select(QualifiedTable),
            .. ScheduledRunTables(),
        ];

        if (tables.Length == 0)
        {
            return;
        }

        foreach (Guid scopeId in integrationEvent.ScopeIds.Distinct())
        {
            if (await paths.GetPathAsync(scopeId, cancellationToken) is not { } path)
            {
                continue;
            }

            foreach (string table in tables)
            {
                // The table name comes from the EF model, never from input; the values are parameters.
#pragma warning disable EF1002, S2077
                await context.Database.ExecuteSqlRawAsync(
                    $"UPDATE {table} SET {ScopeColumns.ScopePathColumn} = {{0}}::ltree WHERE {ScopeColumns.ScopeIdColumn} = {{1}}",
                    [path, scopeId],
                    cancellationToken);
#pragma warning restore EF1002, S2077
            }
        }
    }

    /// <summary>
    /// A run keeps the path it started in as a plain column, not through the scoped-aggregate feature, and a one-off
    /// run (no task) is reached through it alone: so an opted-in context's run table follows a move like a scoped one.
    /// </summary>
    private IEnumerable<string> ScheduledRunTables() =>
        context is IScheduledTasksDbContext && context.Model.FindEntityType(typeof(ScheduledTaskRun)) is { } runs
            ? [QualifiedTable(runs)]
            : [];

    private static string QualifiedTable(IEntityType entityType) =>
        entityType.GetSchema() is { } schema
            ? $"\"{schema}\".\"{entityType.GetTableName()}\""
            : $"\"{entityType.GetTableName()}\"";
}
