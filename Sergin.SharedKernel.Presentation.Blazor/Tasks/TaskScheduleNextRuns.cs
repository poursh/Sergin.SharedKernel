using Microsoft.Extensions.DependencyInjection;
using Sergin.SharedKernel.Application.Tasks;

namespace Sergin.SharedKernel.Presentation.Blazor.Tasks;

/// <summary>
/// The next fire times of a schedule for a preview, asked of the job engine (<see cref="ITaskEngine.NextOccurrences"/>)
/// so a person sees what the engine will do. The engine is resolved on use, not injected: a host with no scheduled-task
/// module registers none, and the parts that ask are never shown there. Empty when the engine cannot say.
/// </summary>
internal sealed class TaskScheduleNextRuns(IServiceProvider services)
{
    public IReadOnlyList<DateTimeOffset> Next(string cron, string timeZoneId, DateTimeOffset after, int count) =>
        services.GetService<ITaskEngine>() is { } engine ? engine.NextOccurrences(cron, timeZoneId, after, count) : [];
}
