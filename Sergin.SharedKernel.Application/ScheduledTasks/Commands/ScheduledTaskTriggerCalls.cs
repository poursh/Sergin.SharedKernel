using Microsoft.Extensions.Logging;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Commands;

/// <summary>
/// The engine call a management handler makes before its save, and the call that undoes it when the save then fails,
/// so a trigger never outlives a refused save. An engine that throws is reported as
/// <see cref="ScheduledTaskErrors.TriggerEngineUnavailable"/> and nothing is saved.
/// </summary>
internal static partial class ScheduledTaskTriggerCalls
{
    /// <summary>Runs <paramref name="call"/>; false (logged) when the engine threw.</summary>
    public static async Task<bool> TryAsync(Func<Task> call, ILogger logger, string action)
    {
        ArgumentNullException.ThrowIfNull(call);

        try
        {
            await call();
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogEngineFailed(logger, exception, action);
            return false;
        }
    }

    /// <summary>
    /// Saves; when the save throws, runs <paramref name="undo"/> (logging, never replacing, its own failure) and lets
    /// the save's exception through, so the pipeline answers it as it would any other: 412, Forbidden, a 500.
    /// </summary>
    public static async Task SaveOrUndoAsync(IScheduledTaskStore store, Func<Task> undo, ILogger logger, string action, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(undo);

        try
        {
            await store.SaveAsync(ct);
        }
        catch (Exception)
        {
            await TryAsync(undo, logger, "undo " + action);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The job engine failed to {Action} a scheduled task's trigger.")]
    private static partial void LogEngineFailed(ILogger logger, Exception exception, string action);
}
