using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Application.Tasks.Activity;
using Sergin.SharedKernel.Application.Tasks.Commands;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Domain.Tasks;
using Sergin.SharedKernel.Domain.Users;

namespace Sergin.SharedKernel.Application.Tasks;

/// <summary>What starts a run: for whom, where, which command with which arguments, and the picked records.</summary>
internal sealed record TaskRunStart(
    TaskScheduleId? ScheduleId,
    TaskRunTrigger Trigger,
    UserId ActingUserId,
    Guid ScopeId,
    string ScopePath,
    string CommandName,
    string Arguments,
    IReadOnlyCollection<TaskScheduleTarget> Targets,
    IReadOnlyDictionary<Guid, string>? TargetLabels = null);

/// <summary>
/// The one way a run begins, whatever started it (a cron fire, "Run now", a one-off): writes the run, copying the
/// command and its arguments onto it, and one item per target; saves; then hands each item to the engine and stores
/// the engine's job id on it. A run is saved before anything is enqueued, so a job always finds its rows.
/// The engine is resolved when a run starts, not injected: the engine is the adapter's to register, and a host
/// (or a test host) that opts a module in without one must still build its container.
/// </summary>
internal sealed partial class TaskRunStarter(IServiceProvider services, IDateTimeProvider clock, IRunNotifier notifier, ILogger<TaskRunStarter> logger)
{
    public async Task<ErrorOr<Guid>> StartAsync(ITaskStore store, TaskRunStart start, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(start);

        var definition = new TaskRunDefinition(
            start.ScheduleId, start.Trigger, start.ActingUserId, start.ScopeId, start.ScopePath, start.CommandName, start.Arguments);
        var run = TaskRun.Start(definition, start.Targets.Count, clock.UtcNow);
        TaskRunItem[] items = [.. start.Targets.Select(target => TaskRunItem.Create(run.Id, target.TargetId, clock.UtcNow, LabelOf(start, target)))];

        // No targets: nothing will ever finish the run, so it is complete at once rather than open forever.
        if (items.Length == 0)
        {
            run.Complete(0, 0, 0, clock.UtcNow);
        }

        store.AddRun(run, items);

        if (run.ScheduleId is not null)
        {
            // The store's one-Running-run-per-task guarantee: a concurrent starter that got there first wins.
            if (!await store.TrySaveStartedRunAsync(ct))
            {
                return TaskErrors.Overlap;
            }
        }
        else
        {
            await store.SaveAsync(ct);
        }

        var enqueued = new HashSet<Guid>();

        try
        {
            ITaskEngine engine = services.GetRequiredService<ITaskEngine>();

            foreach (TaskRunItem item in items)
            {
                item.LinkJob(await engine.EnqueueRunItemAsync(new TaskRunItemReference(store.Schema, item.Id), ct), clock.UtcNow);
                enqueued.Add(item.Id);
                await store.SaveAsync(ct);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogEngineFailed(exception, run.Id, start.CommandName);

            // The run is already saved as Running: items no job will ever execute are failed, or it would stay open and
            // the overlap rule would skip every later firing of its task.
            foreach (TaskRunItem item in items.Where(item => !enqueued.Contains(item.Id)))
            {
                item.MarkFailed(TaskErrors.EngineUnavailable.Code, TaskErrors.EngineUnavailable.Description, clock.UtcNow);
            }

            await store.SaveAsync(ct);
            await store.CompleteRunIfFinishedAsync(run.Id, clock.UtcNow, ct);
            notifier.Publish(new RunChanged(new TaskRunRef(store.Schema, run.Id), start.ActingUserId));

            return TaskErrors.EngineUnavailable;
        }

        notifier.Publish(new RunChanged(new TaskRunRef(store.Schema, run.Id), start.ActingUserId));

        return run.Id;
    }

    // A label is user data kept as stored, only trimmed and cut; a blank one is none.
    private static string? LabelOf(TaskRunStart start, TaskScheduleTarget target)
    {
        if (start.TargetLabels is null || !start.TargetLabels.TryGetValue(target.TargetId, out string? label) || string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        return CutLabel(label.Trim());
    }

    // At most the column's length, never ending on half a surrogate pair: the cut backs off one char instead.
    internal static string CutLabel(string label)
    {
        int max = TaskRunItem.MaxTargetLabelLength;

        if (label.Length <= max)
        {
            return label;
        }

        return char.IsHighSurrogate(label[max - 1]) ? label[..(max - 1)] : label[..max];
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The job engine failed while enqueueing the items of scheduled run {RunId} ({CommandName}); its unqueued items were failed.")]
    private partial void LogEngineFailed(Exception exception, Guid runId, string commandName);
}
