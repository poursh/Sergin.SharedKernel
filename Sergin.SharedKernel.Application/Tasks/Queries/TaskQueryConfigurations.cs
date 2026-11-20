using Sergin.SharedKernel.Application.Commands.Configuration;

namespace Sergin.SharedKernel.Application.Tasks.Queries;

// Untyped requests: checked on the current scope, and the scope filter is filled from the Manage permission's reach,
// which the raw SQL applies. None of their properties is a field a person sees.
internal sealed class GetTaskSchedulesForTargetQueryConfiguration : ICommandConfiguration<GetTaskSchedulesForTargetQuery>
{
    public void Configure(CommandConfigurationBuilder<GetTaskSchedulesForTargetQuery> builder) =>
        builder.RequirePermissions(TaskPermissions.Manage).NotShown(x => x.Schema).NotShown(x => x.TargetId);
}

internal sealed class GetTaskScheduleByIdQueryConfiguration : ICommandConfiguration<GetTaskScheduleByIdQuery>
{
    public void Configure(CommandConfigurationBuilder<GetTaskScheduleByIdQuery> builder) =>
        builder.RequirePermissions(TaskPermissions.Manage).NotShown(x => x.Schema).NotShown(x => x.Id);
}

internal sealed class GetTaskScheduleRunsQueryConfiguration : ICommandConfiguration<GetTaskScheduleRunsQuery>
{
    public void Configure(CommandConfigurationBuilder<GetTaskScheduleRunsQuery> builder) =>
        builder.RequirePermissions(TaskPermissions.Manage).NotShown(x => x.Schema).NotShown(x => x.ScheduleId);
}

internal sealed class GetTaskRunItemsQueryConfiguration : ICommandConfiguration<GetTaskRunItemsQuery>
{
    public void Configure(CommandConfigurationBuilder<GetTaskRunItemsQuery> builder) =>
        builder.RequirePermissions(TaskPermissions.Manage).NotShown(x => x.Schema).NotShown(x => x.RunId);
}

internal sealed class GetOneOffRunsForTargetQueryConfiguration : ICommandConfiguration<GetOneOffRunsForTargetQuery>
{
    public void Configure(CommandConfigurationBuilder<GetOneOffRunsForTargetQuery> builder) =>
        builder.RequirePermissions(TaskPermissions.Manage).NotShown(x => x.Schema).NotShown(x => x.TargetId).NotShown(x => x.Take);
}
