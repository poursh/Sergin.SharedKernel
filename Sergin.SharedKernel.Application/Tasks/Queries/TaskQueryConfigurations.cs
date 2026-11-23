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

internal sealed class GetRecentRunsForTargetQueryConfiguration : ICommandConfiguration<GetRecentRunsForTargetQuery>
{
    public void Configure(CommandConfigurationBuilder<GetRecentRunsForTargetQuery> builder) =>
        builder.RequirePermissions(TaskPermissions.Manage).NotShown(x => x.Schema).NotShown(x => x.TargetId).NotShown(x => x.Take);
}

internal sealed class GetTaskSchedulesForCollectionQueryConfiguration : ICommandConfiguration<GetTaskSchedulesForCollectionQuery>
{
    public void Configure(CommandConfigurationBuilder<GetTaskSchedulesForCollectionQuery> builder) =>
        builder.RequirePermissions(TaskPermissions.Manage).NotShown(x => x.Schema).NotShown(x => x.TaskName).NotShown(x => x.ScopeId);
}

internal sealed class GetRecentRunsForCollectionQueryConfiguration : ICommandConfiguration<GetRecentRunsForCollectionQuery>
{
    public void Configure(CommandConfigurationBuilder<GetRecentRunsForCollectionQuery> builder) =>
        builder.RequirePermissions(TaskPermissions.Manage)
            .NotShown(x => x.Schema).NotShown(x => x.TaskName).NotShown(x => x.ScopeId).NotShown(x => x.Take);
}

internal sealed class GetTaskRunActorsQueryConfiguration : ICommandConfiguration<GetTaskRunActorsQuery>
{
    public void Configure(CommandConfigurationBuilder<GetTaskRunActorsQuery> builder) =>
        builder.RequirePermissions(TaskPermissions.Manage).NotShown(x => x.Schema);
}

// The run list's fields are its list fields (TaskRunListFields), named in its own category.
internal sealed class GetTaskRunsQueryConfiguration : ICommandConfiguration<GetTaskRunsQuery>
{
    public void Configure(CommandConfigurationBuilder<GetTaskRunsQuery> builder) =>
        builder.RequirePermissions(TaskPermissions.Manage).Texts(TaskTexts.RunList.Category).NotShown(x => x.Schema);
}
