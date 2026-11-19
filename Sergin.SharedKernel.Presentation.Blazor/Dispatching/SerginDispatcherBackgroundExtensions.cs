using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.ScheduledTasks.Commands;

namespace Sergin.SharedKernel.Presentation.Blazor.Dispatching;

/// <summary>Starts a background command: the send a page makes instead of sending the command itself, which is refused.</summary>
public static class SerginDispatcherBackgroundExtensions
{
    /// <summary>
    /// Starts <paramref name="template"/> over <paramref name="targets"/> as one background run, answering its reference
    /// as soon as the items are enqueued. The template's own id (and a child's root id) is ignored: each target supplies it.
    /// </summary>
    public static Task<ErrorOr<BackgroundRunStartedResponse>> StartAsync(
        this ISerginDispatcher dispatcher,
        IBaseCommand template,
        IReadOnlyCollection<BackgroundTarget> targets,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        return dispatcher.SendAsync(new StartBackgroundCommand(template, targets), ct);
    }
}
