using Sergin.SharedKernel.Application.Aggregates;

namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>
/// Every configuration scheduled tasks bring to a host that opts in: the aggregate's features and, as they arrive,
/// the management requests' command configurations. <c>AddModuleDbContext</c> registers them as one configuration
/// source, once per host however many contexts opt in. Append new configuration types here.
/// </summary>
public static class ScheduledTaskConfigurationTypes
{
    public static IReadOnlyList<Type> All { get; } =
    [
        typeof(ScheduledTaskAggregateFeatureConfiguration),
    ];

    /// <summary>The aggregate feature configurations among <see cref="All"/>, which an opting-in context applies.</summary>
    public static IReadOnlyList<Type> AggregateConfigurations { get; } = [.. All.Where(AggregateFeatureRegistry.IsConfigurationType)];
}
