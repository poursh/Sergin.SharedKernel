using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.ScheduledTasks;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Domain.ScheduledTasks;
using TickerQ.Utilities.Base;

namespace Sergin.SharedKernel.Infrastructure.ScheduledTasks.TickerQ;

/// <summary>
/// The only two TickerQ functions Sergin declares; each hands its payload to <see cref="IScheduledTaskRunner"/> in a
/// fresh scope and decides nothing itself. A run item the runner asks to try again gets a new job, due after Sergin's
/// <see cref="ScheduledTaskOptions.RetryIntervalsSeconds"/> for the attempt just made (the last interval repeats), and
/// the item is linked to it. The registration is the source generator's: its module initializer in this assembly queues
/// both functions as soon as the assembly loads.
/// </summary>
public sealed class SerginTickerFunctions(IServiceScopeFactory scopes)
{
    public const string Fire = "Sergin.ScheduledTasks.Fire";

    public const string Execute = "Sergin.ScheduledTasks.Execute";

    [TickerFunction(Fire)]
    public async Task FireAsync(TickerFunctionContext<ScheduledTriggerRequest> context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ScheduledTriggerRequest request = context.Request;

        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IScheduledTaskRunner>()
            .FireAsync(request.Schema, new ScheduledTaskId(request.TaskId), cancellationToken);
    }

    [TickerFunction(Execute)]
    public async Task ExecuteAsync(TickerFunctionContext<ScheduledRunItemReference> context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ScheduledRunItemReference item = context.Request;

        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;

        if (!await services.GetRequiredService<IScheduledTaskRunner>().ExecuteRunItemAsync(item.Schema, item.RunItemId, cancellationToken))
        {
            return;
        }

        IScheduledTaskStore store = services.GetRequiredService<IScheduledTaskStoreResolver>().For(item.Schema);
        (ScheduledTaskRun Run, ScheduledTaskRunItem Item)? found = await store.GetRunItemAsync(item.RunItemId, cancellationToken);
        IReadOnlyList<int> intervals = services.GetRequiredService<IOptions<ScheduledTaskOptions>>().Value.RetryIntervalsSeconds;
        int attempts = found?.Item.Attempts ?? 1;
        int seconds = intervals[Math.Clamp(attempts - 1, 0, intervals.Count - 1)];
        DateTime due = services.GetRequiredService<IDateTimeProvider>().UtcNow.AddSeconds(seconds);

        Guid job = await services.GetRequiredService<TickerQScheduledTaskEngine>().EnqueueRunItemAsync(item, due, cancellationToken);

        if (found is { Item: { } retried })
        {
            retried.LinkJob(job);
            await store.SaveAsync(cancellationToken);
        }
    }
}
