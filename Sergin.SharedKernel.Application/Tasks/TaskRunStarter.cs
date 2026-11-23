using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Application.Securities.Authorization;
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
/// the engine's job id on it. A run is saved before anything is enqueued, so a job always finds its rows. A collection
/// task's run has one item with no target, and is refused with <see cref="TaskErrors.AlreadyRunning"/> while a run of
/// the task is Running in the same scope: checked first (advisory), then held by the store's unique index.
/// The engine is resolved when a run starts, not injected: the engine is the adapter's to register, and a host
/// (or a test host) that opts a module in without one must still build its container.
/// </summary>
internal sealed partial class TaskRunStarter(
    IServiceProvider services, TaskCatalog catalog, IDateTimeProvider clock, IRunNotifier notifier, ILogger<TaskRunStarter> logger)
{
    /// <summary>What a run of <paramref name="command"/> targets; a name the catalogue no longer holds is taken as an entity task.</summary>
    internal static TaskRunTargetKind KindOf(TaskDescriptor? command) =>
        command?.TargetKind == CommandTargetKind.Collection ? TaskRunTargetKind.Collection : TaskRunTargetKind.Entity;

    /// <summary>The target of each item: one per picked record, or one null for a collection task, which takes no record.</summary>
    internal static IReadOnlyList<Guid?> ItemTargetsOf(TaskRunTargetKind kind, IReadOnlyCollection<TaskScheduleTarget> targets) =>
        kind == TaskRunTargetKind.Collection ? [null] : [.. targets.Select(target => (Guid?)target.TargetId)];

    public async Task<ErrorOr<Guid>> StartAsync(ITaskStore store, TaskRunStart start, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(start);

        TaskDescriptor? command = catalog.ByName(start.CommandName);
        TaskRunTargetKind kind = KindOf(command);
        bool collection = kind == TaskRunTargetKind.Collection;

        // Advisory: two starts may both pass it, and the unique index below then refuses the second insert.
        if (collection && await store.HasRunningCollectionRunAsync(start.CommandName, start.ScopeId, ct))
        {
            return await AlreadyRunningAsync(start, command!, ct);
        }

        var definition = new TaskRunDefinition(
            start.ScheduleId, start.Trigger, start.ActingUserId, start.ScopeId, start.ScopePath, start.CommandName, start.Arguments, kind);
        IReadOnlyList<Guid?> targets = ItemTargetsOf(kind, start.Targets);
        var run = TaskRun.Start(definition, targets.Count, clock.UtcNow);
        TaskRunItem[] items = [.. targets.Select(target => TaskRunItem.Create(run.Id, target, clock.UtcNow, LabelOf(start, target)))];

        // No targets: nothing will ever finish the run, so it is complete at once rather than open forever.
        if (items.Length == 0)
        {
            run.Complete(0, 0, 0, clock.UtcNow);
        }

        store.AddRun(run, items);

        if (run.ScheduleId is not null || collection)
        {
            // The store's one-Running-run guarantees (per schedule, and per collection task and scope): a concurrent
            // starter that got there first wins.
            StartedRunSave saved = await store.TrySaveStartedRunAsync(ct);

            if (saved == StartedRunSave.ScheduleRunning)
            {
                return TaskErrors.Overlap;
            }

            if (saved == StartedRunSave.CollectionRunning)
            {
                return await AlreadyRunningAsync(start, command!, ct);
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

    /// <summary>
    /// The refusal, naming the task by its view label (a presentation's <see cref="ITaskLabels"/>; else its task name) and
    /// the scope by its name among the scopes the caller can work in (else its path: a cron fire has no caller to ask for).
    /// </summary>
    private async Task<Error> AlreadyRunningAsync(TaskRunStart start, TaskDescriptor command, CancellationToken ct)
    {
        string task = services.GetService<ITaskLabels>()?.LabelOf(command) ?? start.CommandName;

        return TaskErrors.AlreadyRunning(task, await ScopeNameAsync(start, ct));
    }

    private async Task<string> ScopeNameAsync(TaskRunStart start, CancellationToken ct)
    {
        if (services.GetService<IScopeDirectory>() is not { } directory)
        {
            return start.ScopePath;
        }

        try
        {
            IReadOnlyList<ScopeOption> scopes = await directory.GetReachableAsync(ct);

            return scopes.FirstOrDefault(scope => scope.Id == start.ScopeId)?.Name ?? start.ScopePath;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Naming the scope is only for the message: the refusal stands, named by the path.
            LogScopeNameFailed(exception, start.ScopeId);
            return start.ScopePath;
        }
    }

    // A label is user data kept as stored, only trimmed and cut; a blank one is none.
    private static string? LabelOf(TaskRunStart start, Guid? target)
    {
        if (target is not { } id || start.TargetLabels is null || !start.TargetLabels.TryGetValue(id, out string? label) || string.IsNullOrWhiteSpace(label))
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "The scope {ScopeId} could not be named for a task's already-running refusal; its path names it.")]
    private partial void LogScopeNameFailed(Exception exception, Guid scopeId);

    [LoggerMessage(Level = LogLevel.Error, Message = "The job engine failed while enqueueing the items of scheduled run {RunId} ({CommandName}); its unqueued items were failed.")]
    private partial void LogEngineFailed(Exception exception, Guid runId, string commandName);
}
