namespace Sergin.SharedKernel.Application.Tasks;

/// <summary>
/// Tells a request whether it is being sent as one task run item. Set only by the run-item executor in the scope it
/// opens for the send; null everywhere else, so a task sent any other way can be refused.
/// </summary>
public interface IRunItemContext
{
    /// <summary>The run item this scope is executing, or null outside a run item.</summary>
    Guid? RunItemId { get; }

    /// <summary>
    /// The type of the task request the run item sends, set with <see cref="RunItemId"/>: the only task a send inside the
    /// run item may carry, so a task's handler cannot send another task inline.
    /// </summary>
    Type? RequestType { get; }
}

internal sealed class RunItemContext : IRunItemContext
{
    public Guid? RunItemId { get; private set; }

    public Type? RequestType { get; private set; }

    /// <summary>The schema of the module whose store holds the item; set with <see cref="RunItemId"/>.</summary>
    public string? Schema { get; private set; }

    /// <summary>The item's run and the user it acts for: what a progress report's change notice names.</summary>
    public (Guid RunId, UserId ActingUser)? Run { get; private set; }

    /// <summary>
    /// The send's timeouts, which every <see cref="ITaskProgress"/> call resets; null when the send has none (a stored
    /// name no task declares any more never reaches a handler).
    /// </summary>
    public TaskTimeouts? Timeouts { get; private set; }

    public void Set(Guid runItemId, Type requestType, string schema, Guid runId, UserId actingUser, TaskTimeouts? timeouts)
    {
        RunItemId = runItemId;
        RequestType = requestType;
        Schema = schema;
        Run = (runId, actingUser);
        Timeouts = timeouts;
    }
}
