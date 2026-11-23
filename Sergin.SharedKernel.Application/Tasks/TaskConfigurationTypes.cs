using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Application.Tasks.Activity;
using Sergin.SharedKernel.Application.Tasks.Commands;
using Sergin.SharedKernel.Application.Tasks.Queries;

namespace Sergin.SharedKernel.Application.Tasks;

/// <summary>
/// Every configuration tasks bring to a host that opts in: the aggregate's features and, as they arrive,
/// the management requests' command configurations. <c>AddModuleDbContext</c> registers them as one configuration
/// source, once per host however many contexts opt in. Append new configuration types here.
/// </summary>
public static class TaskConfigurationTypes
{
    public static IReadOnlyList<Type> All { get; } =
    [
        typeof(TaskScheduleAggregateFeatureConfiguration),
        typeof(GetTaskSchedulesForTargetQueryConfiguration),
        typeof(GetTaskScheduleByIdQueryConfiguration),
        typeof(GetTaskScheduleRunsQueryConfiguration),
        typeof(GetTaskRunItemsQueryConfiguration),
        typeof(GetRecentRunsForTargetQueryConfiguration),
        typeof(GetTaskSchedulesForCollectionQueryConfiguration),
        typeof(GetRecentRunsForCollectionQueryConfiguration),
        typeof(CreateTaskScheduleCommandConfiguration),
        typeof(UpdateTaskScheduleCommandConfiguration),
        typeof(PauseTaskScheduleCommandConfiguration),
        typeof(ResumeTaskScheduleCommandConfiguration),
        typeof(DeleteTaskScheduleCommandConfiguration),
        typeof(RunTaskScheduleNowCommandConfiguration),
        typeof(RunTaskOnceCommandConfiguration),
        typeof(StartTaskCommandConfiguration),
        typeof(CancelTaskRunCommandConfiguration),
        typeof(GetMyActivityQueryConfiguration),
        typeof(GetTaskRunsQueryConfiguration),
        typeof(GetTaskRunActorsQueryConfiguration),
    ];

    /// <summary>The aggregate feature configurations among <see cref="All"/>, which an opting-in context applies.</summary>
    public static IReadOnlyList<Type> AggregateConfigurations { get; } = [.. All.Where(AggregateFeatureRegistry.IsConfigurationType)];
}
