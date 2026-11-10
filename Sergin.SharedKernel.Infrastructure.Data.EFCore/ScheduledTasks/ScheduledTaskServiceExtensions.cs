using Microsoft.Extensions.DependencyInjection;
using Sergin.SharedKernel.Application.ScheduledTasks;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.ScheduledTasks;

/// <summary>
/// The scheduled-task services a host needs once any module opts in: the run-item executor and the store resolver.
/// <c>AddModuleDbContext</c> calls it from its opt-in branch, beside the context's own keyed store; a marker makes the
/// second opted-in context's call a no-op. The engine (<see cref="IScheduledTaskEngine"/>) is the adapter's to
/// register, and <see cref="ScheduledTaskOptions"/> is bound and validated by <c>AddSerginCore</c> in every host.
/// <para>
/// Lives here rather than in <c>Sergin.SharedKernel.Hosts</c> because <c>AddModuleDbContext</c> is here, and this
/// project cannot reference Hosts (Hosts references it).
/// </para>
/// </summary>
public static class ScheduledTaskServiceExtensions
{
    public static IServiceCollection AddSerginScheduledTasks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(ScheduledTaskServicesMarker)))
        {
            return services;
        }

        services.AddSingleton<ScheduledTaskServicesMarker>();

        // Scoped: each holds the scope's stores, whose contexts are scoped. The executor opens its own child scope for
        // the send, so its bookkeeping context and the command's are never the same one.
        services.AddScoped<IScheduledTaskStoreResolver, ScheduledTaskStoreResolver>();
        services.AddScoped<ScheduledRunItemExecutor>();

        return services;
    }

    private sealed class ScheduledTaskServicesMarker;
}
