using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Sergin.SharedKernel.Application.Aggregates;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;

/// <summary>
/// Turns a context's <see cref="AggregateFeatureRegistry"/> into model shape. A model-finalizing convention
/// runs once, after OnModelCreating, every IEntityTypeConfiguration and the other conventions, on the
/// complete model, so a module writes no mapping for the audit columns. Column names are set explicitly
/// rather than left to UseSnakeCaseNamingConvention, which is not guaranteed to react to properties added
/// this late; the names are the same either way.
/// </summary>
internal sealed class AggregateFeatureConvention(AggregateFeatureRegistry registry) : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (IConventionEntityType entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            if (!registry.For(entityType.ClrType).Audited)
            {
                continue;
            }

            IConventionEntityTypeBuilder builder = entityType.Builder;

            AddAuditProperty(builder, typeof(DateTime), AuditColumns.CreatedAtUtc, AuditColumns.CreatedAtUtcColumn, required: true);
            AddAuditProperty(builder, typeof(Guid), AuditColumns.CreatedBy, AuditColumns.CreatedByColumn, required: true);
            AddAuditProperty(builder, typeof(DateTime?), AuditColumns.ModifiedAtUtc, AuditColumns.ModifiedAtUtcColumn, required: false);
            AddAuditProperty(builder, typeof(Guid?), AuditColumns.ModifiedBy, AuditColumns.ModifiedByColumn, required: false);

            builder.HasAnnotation(AuditColumns.AuditedAnnotation, true);
        }
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
