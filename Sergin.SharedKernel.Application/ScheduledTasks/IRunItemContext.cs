namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>
/// Tells a request whether it is being sent as one scheduled run item. Set only by the run-item executor in the scope it
/// opens for the send; null everywhere else, so a background command sent any other way can be refused.
/// </summary>
public interface IRunItemContext
{
    /// <summary>The run item this scope is executing, or null outside a run item.</summary>
    Guid? RunItemId { get; }
}

internal sealed class RunItemContext : IRunItemContext
{
    public Guid? RunItemId { get; private set; }

    public void Set(Guid runItemId) => RunItemId = runItemId;
}
