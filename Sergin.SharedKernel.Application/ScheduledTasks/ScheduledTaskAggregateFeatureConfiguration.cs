using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>
/// The scheduled task's features. Public, unlike a module's configurations, because it is SharedKernel's: an opting-in
/// module's context applies it in its <c>AggregateFeatures</c> (see <c>IScheduledTasksDbContext</c>), and
/// <c>AddModuleDbContext</c> hands it to the host through <see cref="ScheduledTaskConfigurationTypes"/>.
/// </summary>
public sealed class ScheduledTaskAggregateFeatureConfiguration : IAggregateFeatureConfiguration<ScheduledTask>
{
    public void Configure(AggregateFeatureBuilder<ScheduledTask> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Audited().SoftDeletable().Versioned().Scoped()
            .Permissions(ScheduledTaskPermissions.Manage)
            .Texts(ScheduledTaskTexts.Category)
            .Named(ScheduledTaskTexts.Singular);
    }
}
