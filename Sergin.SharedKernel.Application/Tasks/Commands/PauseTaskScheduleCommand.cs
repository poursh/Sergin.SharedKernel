using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Domain.Tasks;

namespace Sergin.SharedKernel.Application.Tasks.Commands;

/// <summary>Stops a schedule firing, keeping it and its history. Version-guarded by its type.</summary>
public sealed record PauseTaskScheduleCommand(string Schema, TaskScheduleId Id) : ICommand, ITransitionCommand<TaskSchedule, TaskScheduleId>;

internal sealed class PauseTaskScheduleCommandHandler(ITaskStoreResolver stores, TaskEngineAccessor engines, ILogger<PauseTaskScheduleCommandHandler> logger)
    : ICommandHandler<PauseTaskScheduleCommand>
{
    public Task<ErrorOr<Success>> Handle(PauseTaskScheduleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return TaskScheduleStateChange.ApplyAsync(stores.For(request.Schema), request.Id, enabled: false, engines.Engine, logger, cancellationToken);
    }
}

/// <summary>Pause and resume: the state, then the trigger's, then the save, the trigger put back if the save fails.</summary>
internal static class TaskScheduleStateChange
{
    public static async Task<ErrorOr<Success>> ApplyAsync(
        ITaskStore store, TaskScheduleId id, bool enabled, ITaskEngine engine, ILogger logger, CancellationToken ct)
    {
        if (await store.GetTaskAsync(id, ignoreScope: false, ct) is not { } task)
        {
            return Error.NotFound();
        }

        if (enabled)
        {
            task.Resume();
        }
        else
        {
            task.Pause();
        }

        if (task.EngineTriggerId is not { } triggerId)
        {
            await store.SaveAsync(ct);
            return Result.Success;
        }

        string action = enabled ? "resume" : "pause";

        if (!await TaskScheduleTriggerCalls.TryAsync(() => engine.SetEnabledAsync(triggerId, enabled, ct), logger, action))
        {
            return TaskErrors.TriggerEngineUnavailable;
        }

        await TaskScheduleTriggerCalls.SaveOrUndoAsync(
            store, () => engine.SetEnabledAsync(triggerId, !enabled, CancellationToken.None), logger, action, ct);

        return Result.Success;
    }
}
