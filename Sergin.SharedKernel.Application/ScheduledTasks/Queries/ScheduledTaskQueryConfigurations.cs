using Sergin.SharedKernel.Application.Commands.Configuration;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Queries;

// Untyped requests: checked on the current scope, and the scope filter is filled from the Manage permission's reach,
// which the raw SQL applies. None of their properties is a field a person sees.
internal sealed class GetScheduledTasksForTargetQueryConfiguration : ICommandConfiguration<GetScheduledTasksForTargetQuery>
{
    public void Configure(CommandConfigurationBuilder<GetScheduledTasksForTargetQuery> builder) =>
        builder.RequirePermissions(ScheduledTaskPermissions.Manage).NotShown(x => x.Schema).NotShown(x => x.TargetId);
}

internal sealed class GetScheduledTaskByIdQueryConfiguration : ICommandConfiguration<GetScheduledTaskByIdQuery>
{
    public void Configure(CommandConfigurationBuilder<GetScheduledTaskByIdQuery> builder) =>
        builder.RequirePermissions(ScheduledTaskPermissions.Manage).NotShown(x => x.Schema).NotShown(x => x.Id);
}

internal sealed class GetScheduledTaskRunsQueryConfiguration : ICommandConfiguration<GetScheduledTaskRunsQuery>
{
    public void Configure(CommandConfigurationBuilder<GetScheduledTaskRunsQuery> builder) =>
        builder.RequirePermissions(ScheduledTaskPermissions.Manage).NotShown(x => x.Schema).NotShown(x => x.TaskId);
}

internal sealed class GetScheduledTaskRunItemsQueryConfiguration : ICommandConfiguration<GetScheduledTaskRunItemsQuery>
{
    public void Configure(CommandConfigurationBuilder<GetScheduledTaskRunItemsQuery> builder) =>
        builder.RequirePermissions(ScheduledTaskPermissions.Manage).NotShown(x => x.Schema).NotShown(x => x.RunId);
}

internal sealed class GetOneOffRunsForTargetQueryConfiguration : ICommandConfiguration<GetOneOffRunsForTargetQuery>
{
    public void Configure(CommandConfigurationBuilder<GetOneOffRunsForTargetQuery> builder) =>
        builder.RequirePermissions(ScheduledTaskPermissions.Manage).NotShown(x => x.Schema).NotShown(x => x.TargetId).NotShown(x => x.Take);
}
