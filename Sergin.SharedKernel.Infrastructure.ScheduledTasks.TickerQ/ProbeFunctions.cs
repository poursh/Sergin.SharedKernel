using TickerQ.Utilities.Base;

namespace Sergin.SharedKernel.Infrastructure.ScheduledTasks.TickerQ;

/// <summary>Gate probe: proves a function declared in a referenced library runs. Deleted in Task 8.</summary>
public sealed class ProbeFunctions
{
    private static int calls;

    public static int Calls => Volatile.Read(ref calls);

    [TickerFunction("Sergin.Probe")]
    public Task ProbeAsync(TickerFunctionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref calls);
        return Task.CompletedTask;
    }
}
