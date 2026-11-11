using Microsoft.Extensions.DependencyInjection;
using Sergin.SharedKernel.Application.ScheduledTasks;
using Sergin.SharedKernel.Domain.ScheduledTasks;
using TickerQ.Utilities.Base;

namespace Sergin.SharedKernel.Infrastructure.ScheduledTasks.TickerQ;

/// <summary>
/// The only two TickerQ functions Sergin declares; each hands its payload to <see cref="IScheduledTaskRunner"/> in a
/// fresh scope and decides nothing itself. A run item the runner asks to try again gets a new TickerQ job, due after
/// Sergin's retry interval, through <see cref="RunItemDelivery"/>, which the in-process stand-in shares. The registration
/// is the source generator's: its module initializer in this assembly queues both functions as soon as the assembly loads.
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

        await RunItemDelivery.DeliverAsync(
            services,
            item,
            (retried, dueUtc, ct) => services.GetRequiredService<TickerQScheduledTaskEngine>().EnqueueRunItemAsync(retried, dueUtc, ct),
            cancellationToken);
    }
}
