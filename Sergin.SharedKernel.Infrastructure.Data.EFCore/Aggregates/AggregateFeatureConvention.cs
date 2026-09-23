using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;

/// <summary>
/// Turns a context's <see cref="AggregateFeatureRegistry"/> into model shape. A model-finalizing convention
/// runs once, after OnModelCreating, every IEntityTypeConfiguration and the other conventions, on the
/// complete model, so a module writes no mapping for the audit columns. Column names are set explicitly
/// rather than left to UseSnakeCaseNamingConvention, which is not guaranteed to react to properties added
/// this late; the names are the same either way.
/// <para>
/// An aggregate's children are found here, where the relationships are known: starting at each configured
/// root, every navigation from a principal to its dependents (ownership included) is followed, stopping at
/// another aggregate root. Navigations, not foreign keys, decide membership, so another aggregate's child
/// that merely holds a key to this root is not pulled in. Each child reached takes
/// <see cref="AggregateFeatureRegistry.ForChild"/>. An owned type stored in its owner's table gets no columns
/// of its own (the owner's row carries them), but the walk still continues below it.
/// </para>
/// </summary>
internal sealed class AggregateFeatureConvention(AggregateFeatureRegistry registry) : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        Dictionary<IConventionEntityType, (Type Root, AggregateFeatures Features)> resolved = [];

        foreach (Type rootType in registry.ConfiguredTypes)
        {
            // An unmapped root is the startup guard's to report, which sees every module's model at once.
            if (modelBuilder.Metadata.FindEntityType(rootType) is { } root)
            {
                Resolve(root, resolved);
            }
        }

        foreach ((IConventionEntityType entityType, (_, AggregateFeatures features)) in resolved)
        {
            if (features.Audited && !SharesItsOwnersTable(entityType))
            {
                AddAuditProperties(entityType.Builder);
            }
        }
    }

    private void Resolve(
        IConventionEntityType root,
        Dictionary<IConventionEntityType, (Type Root, AggregateFeatures Features)> resolved)
    {
        Type rootType = root.ClrType;
        Assign(root, rootType, registry.For(rootType), resolved);

        HashSet<IConventionEntityType> visited = [root];
        HashSet<Type> reached = [];
        Queue<IConventionEntityType> pending = new([root]);

        while (pending.TryDequeue(out IConventionEntityType? current))
        {
            IEnumerable<IConventionNavigation> toDependents = current
                .GetDerivedTypesInclusive()
                .SelectMany(type => type.GetDeclaredNavigations())
                .Where(navigation => !navigation.IsOnDependent);

            foreach (IConventionNavigation navigation in toDependents)
            {
                IConventionEntityType child = navigation.TargetEntityType;

                if (typeof(IAggregateRoot).IsAssignableFrom(child.ClrType) || !visited.Add(child))
                {
                    continue;
                }

                reached.Add(child.ClrType);
                Assign(child, rootType, registry.ForChild(rootType, child.ClrType), resolved);
                pending.Enqueue(child);
            }
        }

        Type[] strays = [.. registry.ExceptedChildren(rootType).Where(type => !reached.Contains(type))];

        if (strays.Length > 0)
        {
            throw new InvalidOperationException(
                $"The aggregate feature configuration of {rootType.FullName} excepts "
                + $"{string.Join(", ", strays.Select(type => type.FullName))}, which the model does not reach "
                + "through that root's navigations: it is not a child entity of that aggregate.");
        }
    }

    private static void Assign(
        IConventionEntityType entityType,
        Type rootType,
        AggregateFeatures features,
        Dictionary<IConventionEntityType, (Type Root, AggregateFeatures Features)> resolved)
    {
        if (resolved.TryGetValue(entityType, out (Type Root, AggregateFeatures Features) earlier)
            && earlier.Features != features)
        {
            throw new InvalidOperationException(
                $"{entityType.DisplayName()} is reached from both {earlier.Root.FullName} and {rootType.FullName}, "
                + "whose aggregate feature configurations give it different features. An entity belongs to one "
                + "aggregate; make the two agree, or except it in one of them.");
        }

        resolved[entityType] = (rootType, features);
    }

    private static bool SharesItsOwnersTable(IConventionEntityType entityType) =>
        entityType.FindOwnership() is { } ownership
        && entityType.GetTableName() == ownership.PrincipalEntityType.GetTableName()
        && entityType.GetSchema() == ownership.PrincipalEntityType.GetSchema();

    private static void AddAuditProperties(IConventionEntityTypeBuilder builder)
    {
        AddAuditProperty(builder, typeof(DateTime), AuditColumns.CreatedAtUtc, AuditColumns.CreatedAtUtcColumn, required: true);
        AddAuditProperty(builder, typeof(Guid), AuditColumns.CreatedBy, AuditColumns.CreatedByColumn, required: true);
        AddAuditProperty(builder, typeof(DateTime?), AuditColumns.ModifiedAtUtc, AuditColumns.ModifiedAtUtcColumn, required: false);
        AddAuditProperty(builder, typeof(Guid?), AuditColumns.ModifiedBy, AuditColumns.ModifiedByColumn, required: false);

        builder.HasAnnotation(AuditColumns.AuditedAnnotation, true);
    }

    private static void AddAuditProperty(
        IConventionEntityTypeBuilder builder, Type clrType, string name, string column, bool required)
    {
        IConventionPropertyBuilder property = builder.Property(clrType, name)
            ?? throw new InvalidOperationException(
                $"Could not add the audit property {name} to {builder.Metadata.DisplayName()}: "
                + "an explicit mapping already configures a conflicting member of that name.");

        property.IsRequired(required);
        property.HasColumnName(column);
    }
}
