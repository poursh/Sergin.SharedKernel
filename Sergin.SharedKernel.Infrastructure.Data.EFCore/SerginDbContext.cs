using Microsoft.EntityFrameworkCore;
using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore;

public abstract class SerginDbContext(DbContextOptions options) : DbContext(options), IDbContext
{
    /// <summary>
    /// The aggregate feature configurations this context's model applies. A module that opts in overrides it with
    /// <c>AggregateFeatureRegistry.FromAssemblies([&lt;Module&gt;ApplicationAssemblyReference.Assembly])</c>,
    /// expression-bodied so the scan runs only when EF builds the model, once per context type.
    /// <para>
    /// It is a property of the context, not a DI lookup, on purpose: an IDesignTimeDbContextFactory builds
    /// the context with no container, and a registry that came back empty there would make the next
    /// <c>dotnet ef migrations add</c> scaffold DropColumn for every audit column.
    /// </para>
    /// </summary>
    protected virtual AggregateFeatureRegistry AggregateFeatures => AggregateFeatureRegistry.Empty;

    /// <summary>A subclass that overrides this must call base, or its aggregate features are silently dropped.</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        base.ConfigureConventions(configurationBuilder);

        AggregateFeatureRegistry features = AggregateFeatures;
        configurationBuilder.Conventions.Add(_ => new AggregateFeatureConvention(features));
    }

    /// <summary>
    /// A row_version mismatch surfaces as <see cref="ConcurrencyConflictException"/>, which the Application
    /// layer can catch without referencing EF; ExpectedVersionPipelineBehavior turns it into the stale-version
    /// error. The save's transaction has rolled back by then.
    /// </summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException("The aggregate changed since the expected version was read.", exception);
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException("The aggregate changed since the expected version was read.", exception);
        }
    }
}
