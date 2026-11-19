using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Sergin.SharedKernel.Application.ScheduledTasks;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.ScheduledTasks;

/// <summary>Resolves the store <c>AddModuleDbContext</c> registered, keyed by schema, for each opted-in context.</summary>
internal sealed class ScheduledTaskStoreResolver(IServiceProvider services) : IScheduledTaskStoreResolver
{
    public IScheduledTaskStore For(string schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        return services.GetKeyedService<IScheduledTaskStore>(schema)
            ?? throw new InvalidOperationException(
                $"No module opted into scheduled tasks under schema \"{schema}\": its DbContext must implement IScheduledTasksDbContext.");
    }

    public bool TryFor(string schema, [NotNullWhen(true)] out IScheduledTaskStore? store)
    {
        ArgumentNullException.ThrowIfNull(schema);

        store = services.GetKeyedService<IScheduledTaskStore>(schema);

        return store is not null;
    }
}
