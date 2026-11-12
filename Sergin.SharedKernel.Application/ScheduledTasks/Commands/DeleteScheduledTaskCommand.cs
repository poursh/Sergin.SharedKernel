using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Commands;

/// <summary>Soft-deletes a schedule; its runs stay in history. Version-guarded by its type.</summary>
public sealed record DeleteScheduledTaskCommand(string Schema, ScheduledTaskId Id) : ICommand, IDeleteCommand<ScheduledTask, ScheduledTaskId>;

/// <summary>
/// The soft delete is saved first, then the trigger removed. A trigger the engine fails to remove is only logged: the
/// delete happened, and a firing of a deleted task does nothing (the runner finds no live task).
/// </summary>
internal sealed partial class DeleteScheduledTaskCommandHandler(
    IScheduledTaskStoreResolver stores, ScheduledTaskEngineAccessor engines, ILogger<DeleteScheduledTaskCommandHandler> logger)
    : ICommandHandler<DeleteScheduledTaskCommand>
{
    public async Task<ErrorOr<Success>> Handle(DeleteScheduledTaskCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        IScheduledTaskEngine engine = engines.Engine;

        IScheduledTaskStore store = stores.For(request.Schema);

        if (await store.GetTaskAsync(request.Id, ignoreScope: false, cancellationToken) is not { } task)
        {
            return Error.NotFound();
        }

        store.RemoveTask(task);
        await store.SaveAsync(cancellationToken);

        if (task.EngineTriggerId is { } triggerId
            && !await ScheduledTaskTriggerCalls.TryAsync(() => engine.RemoveAsync(triggerId, CancellationToken.None), logger, "remove"))
        {
            LogTriggerLeft(task.Id.Value, triggerId);
        }

        return Result.Success;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scheduled task {TaskId} was deleted but its trigger {TriggerId} is still in the engine; its firings do nothing.")]
    private partial void LogTriggerLeft(Guid taskId, Guid triggerId);
}
