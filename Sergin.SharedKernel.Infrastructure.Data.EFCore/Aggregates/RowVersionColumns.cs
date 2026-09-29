using Microsoft.EntityFrameworkCore.Metadata;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;

/// <summary>
/// The one spelling of the row-version shadow property, its column and its annotations, shared by the
/// convention that adds them, the interceptors that check and rewrite them, and every raw-SQL GetOne that
/// selects the column. A versioned root carries <see cref="VersionedAnnotation"/>; each of its child entity
/// types carries <see cref="VersionRootAnnotation"/> holding the root entity type's name. A string, not a
/// Type: annotations are written into the migrations model snapshot.
/// </summary>
public static class RowVersionColumns
{
    public const string VersionedAnnotation = "Sergin:Versioned";
    public const string VersionRootAnnotation = "Sergin:VersionRoot";

    public const string RowVersion = "RowVersion";
    public const string RowVersionColumn = "row_version";

    /// <summary>Walks the <see cref="IReadOnlyEntityType.BaseType"/> chain, for the same reason as <see cref="AuditColumns.IsAudited"/>.</summary>
    public static bool IsVersioned(IReadOnlyEntityType entityType) =>
        Find(entityType, VersionedAnnotation) is not null;

    /// <summary>The name of the versioned root <paramref name="entityType"/> is a child of; null when it is none.</summary>
    public static string? RootOf(IReadOnlyEntityType entityType) =>
        Find(entityType, VersionRootAnnotation) as string;

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
