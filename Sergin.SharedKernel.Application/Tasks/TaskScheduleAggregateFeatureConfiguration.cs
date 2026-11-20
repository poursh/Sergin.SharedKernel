using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Domain.Tasks;

namespace Sergin.SharedKernel.Application.Tasks;

/// <summary>
/// The scheduled task's features. Public, unlike a module's configurations, because it is SharedKernel's: an opting-in
/// module's context applies it in its <c>AggregateFeatures</c> (see <c>ITasksDbContext</c>), and
/// <c>AddModuleDbContext</c> hands it to the host through <see cref="TaskConfigurationTypes"/>.
/// </summary>
public sealed class TaskScheduleAggregateFeatureConfiguration : IAggregateFeatureConfiguration<TaskSchedule>
{
    public void Configure(AggregateFeatureBuilder<TaskSchedule> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Audited().SoftDeletable().Versioned().Scoped()
            .Permissions(TaskPermissions.Manage)
            .Texts(TaskTexts.Category)
            .Named(TaskTexts.Singular);
    }
}
