using Microsoft.EntityFrameworkCore.Metadata;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;

/// <summary>
/// The one spelling of the soft-delete shadow properties, their columns, the query filter and the SQL
/// predicates, shared by the convention that adds them, the interceptor that stamps them, and every raw-SQL
/// read, which EF's query filter does not reach and must filter with <see cref="NotDeletedSql"/> itself.
/// A row is live exactly when <c>deleted_at_utc</c> is NULL; there is no separate flag.
/// </summary>
public static class SoftDeleteColumns
{
    public const string SoftDeletableAnnotation = "Sergin:SoftDeletable";

    /// <summary>
    /// The name of the EF query filter, so <c>IgnoreQueryFilters([SoftDeleteColumns.QueryFilterName])</c> sees
    /// deleted rows while leaving any other named filter in force.
    /// </summary>
    public const string QueryFilterName = "Sergin:SoftDelete";

    public const string DeletedAtUtc = "DeletedAtUtc";
    public const string DeletedBy = "DeletedBy";

    public const string DeletedAtUtcColumn = "deleted_at_utc";
    public const string DeletedByColumn = "deleted_by";

    /// <summary>The live-row predicate: the filter of every partial unique index, and of every raw-SQL read.</summary>
    public const string NotDeletedSql = DeletedAtUtcColumn + " IS NULL";

    /// <summary>The CHECK constraint that keeps the two columns set together: never a deletion without an actor.</summary>
    public const string PairCheckSql = "(" + DeletedAtUtcColumn + " IS NULL) = (" + DeletedByColumn + " IS NULL)";

    /// <summary>The name of <see cref="PairCheckSql"/>'s constraint on <paramref name="table"/>.</summary>
    public static string PairCheckName(string table) => $"ck_{table}_soft_delete";

    /// <summary>Walks the <see cref="IReadOnlyEntityType.BaseType"/> chain, for the same reason as <see cref="AuditColumns.IsAudited"/>.</summary>
    public static bool IsSoftDeletable(IReadOnlyEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        for (IReadOnlyEntityType? current = entityType; current is not null; current = current.BaseType)
        {
            if (current.FindAnnotation(SoftDeletableAnnotation) is not null)
            {
                return true;
            }
        }

        return false;
    }
}
