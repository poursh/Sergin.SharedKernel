using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Application.Tasks.Commands;

namespace Sergin.SharedKernel.Presentation.Blazor.Dispatching;

/// <summary>Starts a task: the send a page makes instead of sending the task itself, which is refused.</summary>
public static class SerginDispatcherTaskExtensions
{
    /// <summary>
    /// Starts <paramref name="template"/> over <paramref name="targets"/> as one run, answering its reference as soon as the
    /// items are enqueued. The template's own id is ignored: each target supplies it. The constraint keeps a command that
    /// is not a task from being passed at all.
    /// </summary>
    public static Task<ErrorOr<TaskRunStartedResponse>> StartTaskAsync<TTask>(
        this ISerginDispatcher dispatcher,
        TTask template,
        IReadOnlyCollection<TaskTarget> targets,
        CancellationToken ct = default)
        where TTask : ITaskCommand
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        return dispatcher.SendAsync(new StartTaskCommand(template, targets), ct);
    }
}
