using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Sergin.SharedKernel.Application.Tasks;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Tasks;

/// <summary>Resolves the store <c>AddModuleDbContext</c> registered, keyed by schema, for each opted-in context.</summary>
internal sealed class TaskStoreResolver(IServiceProvider services) : ITaskStoreResolver
{
    public ITaskStore For(string schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        return services.GetKeyedService<ITaskStore>(schema)
            ?? throw new InvalidOperationException(
                $"No module opted into scheduled tasks under schema \"{schema}\": its DbContext must implement ITasksDbContext.");
    }

    public bool TryFor(string schema, [NotNullWhen(true)] out ITaskStore? store)
    {
        ArgumentNullException.ThrowIfNull(schema);

        store = services.GetKeyedService<ITaskStore>(schema);

        return store is not null;
    }
}
