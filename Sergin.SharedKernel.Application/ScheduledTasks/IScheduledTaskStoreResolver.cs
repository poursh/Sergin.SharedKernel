namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>Finds the <see cref="IScheduledTaskStore"/> of the module that owns a schema.</summary>
public interface IScheduledTaskStoreResolver
{
    /// <summary>The schema's store; throws <see cref="InvalidOperationException"/> naming it when no module opted in under it.</summary>
    IScheduledTaskStore For(string schema);
}
