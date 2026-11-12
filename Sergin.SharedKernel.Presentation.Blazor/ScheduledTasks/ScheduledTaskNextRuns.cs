using Microsoft.Extensions.DependencyInjection;
using Sergin.SharedKernel.Application.ScheduledTasks;

namespace Sergin.SharedKernel.Presentation.Blazor.ScheduledTasks;

/// <summary>
/// The next fire times of a schedule for a preview, asked of the job engine (<see cref="IScheduledTaskEngine.NextOccurrences"/>)
/// so a person sees what the engine will do. The engine is resolved on use, not injected: a host with no scheduled-task
/// module registers none, and the parts that ask are never shown there. Empty when the engine cannot say.
/// </summary>
internal sealed class ScheduledTaskNextRuns(IServiceProvider services)
{
    public IReadOnlyList<DateTimeOffset> Next(string cron, string timeZoneId, DateTimeOffset after, int count) =>
        services.GetService<IScheduledTaskEngine>() is { } engine ? engine.NextOccurrences(cron, timeZoneId, after, count) : [];
}
