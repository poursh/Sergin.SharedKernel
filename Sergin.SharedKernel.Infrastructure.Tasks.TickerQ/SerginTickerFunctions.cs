using Microsoft.Extensions.DependencyInjection;
using Sergin.SharedKernel.Application.Tasks;
using Sergin.SharedKernel.Domain.Tasks;
using TickerQ.Utilities.Base;

namespace Sergin.SharedKernel.Infrastructure.Tasks.TickerQ;

/// <summary>
/// The only two TickerQ functions Sergin declares; each hands its payload to <see cref="ITaskScheduleRunner"/> in a
/// fresh scope and decides nothing itself. A run item the runner asks to try again gets a new TickerQ job, due after
/// Sergin's retry interval, through <see cref="RunItemDelivery"/>, which the in-process stand-in shares. The registration
/// is the source generator's: its module initializer in this assembly queues both functions as soon as the assembly loads.
/// </summary>
public sealed class SerginTickerFunctions(IServiceScopeFactory scopes)
{
    public const string Fire = "Sergin.Tasks.Fire";

    public const string Execute = "Sergin.Tasks.Execute";

    [TickerFunction(Fire)]
    public async Task FireAsync(TickerFunctionContext<TaskScheduleTriggerRequest> context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        TaskScheduleTriggerRequest request = context.Request;

        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ITaskScheduleRunner>()
            .FireAsync(request.Schema, new TaskScheduleId(request.ScheduleId), cancellationToken);
    }

    [TickerFunction(Execute)]
    public async Task ExecuteAsync(TickerFunctionContext<TaskRunItemReference> context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        TaskRunItemReference item = context.Request;

        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;

        await RunItemDelivery.DeliverAsync(
            services,
            item,
            (retried, dueUtc, ct) => services.GetRequiredService<TickerQTaskEngine>().EnqueueRunItemAsync(retried, dueUtc, ct),
            cancellationToken);
    }
}
