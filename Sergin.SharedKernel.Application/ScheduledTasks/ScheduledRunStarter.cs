using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Domain.ScheduledTasks;
using Sergin.SharedKernel.Domain.Users;

namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>What starts a run: for whom, where, which command with which arguments, and the picked records.</summary>
internal sealed record ScheduledRunStart(
    ScheduledTaskId? TaskId,
    ScheduledRunTrigger Trigger,
    UserId ActingUserId,
    Guid ScopeId,
    string ScopePath,
    string CommandName,
    string Arguments,
    IReadOnlyCollection<ScheduledTaskTarget> Targets);

/// <summary>
/// The one way a run begins, whatever started it (a cron fire, "Run now", a one-off): writes the run, copying the
/// command and its arguments onto it, and one item per target; saves; then hands each item to the engine and stores
/// the engine's job id on it. A run is saved before anything is enqueued, so a job always finds its rows.
/// The engine is resolved when a run starts, not injected: the engine is the adapter's to register, and a host
/// (or a test host) that opts a module in without one must still build its container.
/// </summary>
internal sealed partial class ScheduledRunStarter(IServiceProvider services, IDateTimeProvider clock, ILogger<ScheduledRunStarter> logger)
{
    public async Task<ErrorOr<Guid>> StartAsync(IScheduledTaskStore store, ScheduledRunStart start, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(start);

        var definition = new ScheduledRunDefinition(
            start.TaskId, start.Trigger, start.ActingUserId, start.ScopeId, start.ScopePath, start.CommandName, start.Arguments);
        var run = ScheduledTaskRun.Start(definition, start.Targets.Count, clock.UtcNow);
        ScheduledTaskRunItem[] items = [.. start.Targets.Select(target => ScheduledTaskRunItem.Create(run.Id, target.TargetId, target.RootId, clock.UtcNow))];

        // No targets: nothing will ever finish the run, so it is complete at once rather than open forever.
        if (items.Length == 0)
        {
            run.Complete(0, 0, clock.UtcNow);
        }

        store.AddRun(run, items);

        if (run.TaskId is not null)
        {
            // The store's one-Running-run-per-task guarantee: a concurrent starter that got there first wins.
            if (!await store.TrySaveStartedRunAsync(ct))
            {
                return ScheduledTaskErrors.Overlap;
            }
        }
        else
        {
            await store.SaveAsync(ct);
        }

        var enqueued = new HashSet<Guid>();

        try
        {
            IScheduledTaskEngine engine = services.GetRequiredService<IScheduledTaskEngine>();

            foreach (ScheduledTaskRunItem item in items)
            {
                item.LinkJob(await engine.EnqueueRunItemAsync(new ScheduledRunItemReference(store.Schema, item.Id), ct), clock.UtcNow);
                enqueued.Add(item.Id);
                await store.SaveAsync(ct);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogEngineFailed(exception, run.Id, start.CommandName);

            // The run is already saved as Running: items no job will ever execute are failed, or it would stay open and
            // the overlap rule would skip every later firing of its task.
            foreach (ScheduledTaskRunItem item in items.Where(item => !enqueued.Contains(item.Id)))
            {
                item.MarkFailed(ScheduledTaskErrors.EngineUnavailable.Code, ScheduledTaskErrors.EngineUnavailable.Description, clock.UtcNow);
            }

            await store.SaveAsync(ct);
            await store.CompleteRunIfFinishedAsync(run.Id, clock.UtcNow, ct);

            return ScheduledTaskErrors.EngineUnavailable;
        }

        return run.Id;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The job engine failed while enqueueing the items of scheduled run {RunId} ({CommandName}); its unqueued items were failed.")]
    private partial void LogEngineFailed(Exception exception, Guid runId, string commandName);
}
