using Microsoft.EntityFrameworkCore.Metadata;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;

/// <summary>
/// The one spelling of the audit shadow properties and their columns, shared by the convention that adds
/// them, the interceptor that stamps them, and any raw-SQL read that selects them.
/// <c>modified_*</c> is nullable on purpose: a row that was only inserted was not modified.
/// </summary>
public static class AuditColumns
{
    public const string AuditedAnnotation = "Sergin:Audited";

    public const string CreatedAtUtc = "CreatedAtUtc";
    public const string CreatedBy = "CreatedBy";
    public const string ModifiedAtUtc = "ModifiedAtUtc";
    public const string ModifiedBy = "ModifiedBy";

    public const string CreatedAtUtcColumn = "created_at_utc";
    public const string CreatedByColumn = "created_by";
    public const string ModifiedAtUtcColumn = "modified_at_utc";
    public const string ModifiedByColumn = "modified_by";

    /// <summary>
    /// Walks <paramref name="entityType"/> and its <see cref="IReadOnlyEntityType.BaseType"/> chain, because
    /// EF annotations are not inherited: in a TPH hierarchy, a derived type configured only through its base's
    /// <c>Audited()</c> call shares the base's shadow columns but carries no <see cref="AuditedAnnotation"/> of
    /// its own.
    /// </summary>
    public static bool IsAudited(IReadOnlyEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        for (IReadOnlyEntityType? current = entityType; current is not null; current = current.BaseType)
        {
            if (current.FindAnnotation(AuditedAnnotation) is not null)
            {
                return true;
            }
        }

        return false;
    }
}
