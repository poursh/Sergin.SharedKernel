using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Commands;

/// <summary>Stops a schedule firing, keeping it and its history. Version-guarded by its type.</summary>
public sealed record PauseScheduledTaskCommand(string Schema, ScheduledTaskId Id) : ICommand, ITransitionCommand<ScheduledTask, ScheduledTaskId>;

internal sealed class PauseScheduledTaskCommandHandler(IScheduledTaskStoreResolver stores, ScheduledTaskEngineAccessor engines, ILogger<PauseScheduledTaskCommandHandler> logger)
    : ICommandHandler<PauseScheduledTaskCommand>
{
    public Task<ErrorOr<Success>> Handle(PauseScheduledTaskCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ScheduledTaskStateChange.ApplyAsync(stores.For(request.Schema), request.Id, enabled: false, engines.Engine, logger, cancellationToken);
    }
}

/// <summary>Pause and resume: the state, then the trigger's, then the save, the trigger put back if the save fails.</summary>
internal static class ScheduledTaskStateChange
{
    public static async Task<ErrorOr<Success>> ApplyAsync(
        IScheduledTaskStore store, ScheduledTaskId id, bool enabled, IScheduledTaskEngine engine, ILogger logger, CancellationToken ct)
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

        if (!await ScheduledTaskTriggerCalls.TryAsync(() => engine.SetEnabledAsync(triggerId, enabled, ct), logger, action))
        {
            return ScheduledTaskErrors.TriggerEngineUnavailable;
        }

        await ScheduledTaskTriggerCalls.SaveOrUndoAsync(
            store, () => engine.SetEnabledAsync(triggerId, !enabled, CancellationToken.None), logger, action, ct);

        return Result.Success;
    }
}
