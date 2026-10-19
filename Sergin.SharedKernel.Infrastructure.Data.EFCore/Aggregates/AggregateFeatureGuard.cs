using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Sergin.SharedKernel.Application.Aggregates;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;

/// <summary>
/// Fails host start when a declared aggregate feature configuration does not reach an EF model: its type is mapped
/// by no module DbContext (a configuration stranded in the wrong module), or the context that maps it does
/// not apply it (a module context without an AggregateFeatures override), or a type the model scopes is not
/// Scoped() in the host's registry (the command registry reads the same ConfigurationsAssemblies, so a request
/// bound to it would be checked on any scope). Called by both host bootstraps in
/// every environment, before the Development-only migrate step. Building each context's model needs no
/// database connection.
/// </summary>
public static class AggregateFeatureGuard
{
    public static void EnsureApplied(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        AggregateFeatureRegistry registry = services.GetRequiredService<AggregateFeatureRegistry>();

        using IServiceScope scope = services.CreateScope();

        IReadOnlyCollection<IModel> models =
        [
            .. services.GetServices<ModuleDbContextRegistration>()
                .Select(registration => ((DbContext)scope.ServiceProvider.GetRequiredService(registration.ContextType)).Model)
        ];

        EnsureApplied(registry, models);
    }

    public static void EnsureApplied(AggregateFeatureRegistry registry, IReadOnlyCollection<IModel> models)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(models);

        List<string> problems = [];

        foreach (Type configured in registry.ConfiguredTypes)
        {
            IEntityType[] mappings = [.. models.Select(model => model.FindEntityType(configured)).OfType<IEntityType>()];

            if (mappings.Length == 0)
            {
                problems.Add($"{configured.FullName} is not mapped by any module DbContext");
                continue;
            }

            if (registry.For(configured).Audited && mappings.Any(mapping => !AuditColumns.IsAudited(mapping)))
            {
                problems.Add(
                    $"{configured.FullName} is configured Audited(), but the DbContext that maps it does not apply "
                    + "its module's aggregate feature configurations — override SerginDbContext.AggregateFeatures there");
            }

            if (registry.For(configured).SoftDeletable && mappings.Any(mapping => !SoftDeleteColumns.IsSoftDeletable(mapping)))
            {
                problems.Add(
                    $"{configured.FullName} is configured SoftDeletable(), but the DbContext that maps it does not apply "
                    + "its module's aggregate feature configurations — override SerginDbContext.AggregateFeatures there");
            }

            if (registry.For(configured).Scoped && mappings.Any(mapping => !ScopeColumns.IsScoped(mapping)))
            {
                problems.Add(
                    $"{configured.FullName} is configured Scoped(), but the DbContext that maps it does not apply "
                    + "its module's aggregate feature configurations — override SerginDbContext.AggregateFeatures there");
            }
        }

        // The other direction, for the one feature a permission check reads: a type the EF model scopes but the
        // host's registry does not know as Scoped() would have its requests checked on any scope, not the current one.
        IEnumerable<Type> scopedInModelOnly = models
            .SelectMany(model => model.GetEntityTypes())
            .Where(ScopeColumns.IsScoped)
            .Select(entityType => entityType.ClrType)
            .Distinct()
            .Where(type => !registry.For(type).Scoped);

        foreach (Type type in scopedInModelOnly)
        {
            problems.Add(
                $"{type.FullName} is scoped in the EF model, but no configuration the host reads declares it Scoped() — "
                + "move its aggregate feature configuration into its module's ConfigurationsAssembly");
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"Aggregate feature configuration does not match the EF model: {string.Join("; ", problems)}.");
        }
    }
}
