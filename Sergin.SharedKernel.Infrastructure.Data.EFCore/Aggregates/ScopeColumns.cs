using Dapper;
using Microsoft.EntityFrameworkCore.Metadata;
using Sergin.SharedKernel.Application.Securities.Authorization;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;

/// <summary>
/// The one spelling of the scope shadow properties, their columns, annotations, query filter and SQL predicate,
/// shared by the convention that adds them, the interceptors that stamp and check them, the handler that rewrites
/// paths after a scope move, and every raw-SQL read of a scoped table, which EF's query filter does not reach and
/// must filter with <see cref="ReachSql"/> itself. A scoped root carries <see cref="ScopedAnnotation"/>; each of its
/// child entity types carries <see cref="ScopeRootAnnotation"/> holding the root entity type's name.
/// </summary>
public static class ScopeColumns
{
    public const string ScopedAnnotation = "Sergin:Scoped";
    public const string ScopeRootAnnotation = "Sergin:ScopeRoot";

    /// <summary>
    /// The name of the EF query filter, so <c>IgnoreQueryFilters([ScopeColumns.QueryFilterName])</c> sees every
    /// scope while leaving soft delete in force: what a uniqueness check needs.
    /// </summary>
    public const string QueryFilterName = "Sergin:Scope";

    public const string ScopeId = "ScopeId";
    public const string ScopePath = "ScopePath";

    public const string ScopeIdColumn = "scope_id";
    public const string ScopePathColumn = "scope_path";

    public const string LTreeExtension = "ltree";

    /// <summary>
    /// The read predicate: inside the current scope's subtree, and inside the caller's reach. Its parameters come
    /// from <see cref="AddParameters"/>. A constant, so a query that embeds it stays a constant; its columns are
    /// unqualified, which is unambiguous while only one table in the query is scoped.
    /// </summary>
    public const string ReachSql =
        "@ScopeVisible AND " + ScopePathColumn + " <@ @ScopeCurrentPath::ltree "
        + "AND (@ScopeUnrestricted OR " + ScopePathColumn + " <@ @ScopeSubtreePaths::ltree[] "
        + "OR " + ScopeIdColumn + " = ANY(@ScopeExactIds))";

    /// <summary>
    /// The scope column a raw-SQL read of <c>AuditInfo</c> appends to <c>AuditColumns.SelectSql</c>, aliased to its
    /// constructor parameter so Dapper binds it; <see cref="UnscopedSelectSql"/> for a table that carries none.
    /// </summary>
    public const string SelectSql = ScopeIdColumn + " AS scopeId";

    /// <summary><see cref="SelectSql"/> for a table that is not scoped: a null of the same type.</summary>
    public const string UnscopedSelectSql = "NULL::uuid AS scopeId";

    /// <summary>Adds <see cref="ReachSql"/>'s parameters, read from <paramref name="filter"/>, to <paramref name="parameters"/>.</summary>
    public static DynamicParameters AddParameters(DynamicParameters parameters, ScopeFilter filter)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(filter);

        parameters.Add("ScopeVisible", filter.Visible);
        parameters.Add("ScopeCurrentPath", filter.CurrentPath);
        parameters.Add("ScopeUnrestricted", filter.Unrestricted);
        parameters.Add("ScopeSubtreePaths", filter.SubtreePaths.ToArray());
        parameters.Add("ScopeExactIds", filter.ExactScopeIds.ToArray());

        return parameters;
    }

    /// <summary>Walks the <see cref="IReadOnlyEntityType.BaseType"/> chain, for the same reason as <see cref="AuditColumns.IsAudited"/>.</summary>
    public static bool IsScoped(IReadOnlyEntityType entityType) =>
        Find(entityType, ScopedAnnotation) is not null;

    /// <summary>The name of the scoped root <paramref name="entityType"/> is a child of; null when it is none.</summary>
    public static string? RootOf(IReadOnlyEntityType entityType) =>
        Find(entityType, ScopeRootAnnotation) as string;

    private static object? Find(IReadOnlyEntityType entityType, string annotation)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        for (IReadOnlyEntityType? current = entityType; current is not null; current = current.BaseType)
        {
            if (current.FindAnnotation(annotation) is { } found)
            {
                return found.Value;
            }
        }

        return null;
    }
}
