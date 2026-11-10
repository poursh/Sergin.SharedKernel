using Microsoft.Extensions.DependencyInjection;
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
internal sealed class ScheduledRunStarter(IServiceProvider services, IDateTimeProvider clock)
{
    public async Task<ErrorOr<Guid>> StartAsync(IScheduledTaskStore store, ScheduledRunStart start, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(start);

        var definition = new ScheduledRunDefinition(
            start.TaskId, start.Trigger, start.ActingUserId, start.ScopeId, start.ScopePath, start.CommandName, start.Arguments);
        var run = ScheduledTaskRun.Start(definition, start.Targets.Count, clock.UtcNow);
        ScheduledTaskRunItem[] items = [.. start.Targets.Select(target => ScheduledTaskRunItem.Create(run.Id, target.TargetId, target.RootId))];

        // No targets: nothing will ever finish the run, so it is complete at once rather than open forever.
        if (items.Length == 0)
        {
            run.Complete(0, 0, clock.UtcNow);
        }

        store.AddRun(run, items);
        await store.SaveAsync(ct);

        IScheduledTaskEngine engine = services.GetRequiredService<IScheduledTaskEngine>();

        foreach (ScheduledTaskRunItem item in items)
        {
            item.LinkJob(await engine.EnqueueRunItemAsync(new ScheduledRunItemReference(store.Schema, item.Id), ct));
            await store.SaveAsync(ct);
        }

        return run.Id;
    }
}
