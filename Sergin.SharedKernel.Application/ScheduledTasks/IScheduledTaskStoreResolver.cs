using System.Diagnostics.CodeAnalysis;

namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>Finds the <see cref="IScheduledTaskStore"/> of the module that owns a schema.</summary>
public interface IScheduledTaskStoreResolver
{
    /// <summary>The schema's store; throws <see cref="InvalidOperationException"/> naming it when no module opted in under it.</summary>
    IScheduledTaskStore For(string schema);

    /// <summary>The schema's store, or false when no module opted in under it: for a schema a caller named.</summary>
    bool TryFor(string schema, [NotNullWhen(true)] out IScheduledTaskStore? store);
}
