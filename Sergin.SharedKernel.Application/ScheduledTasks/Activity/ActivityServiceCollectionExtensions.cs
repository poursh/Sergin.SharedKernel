using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Activity;

/// <summary>
/// The activity read: the directory over every <see cref="IBackgroundRunSource"/> and <see cref="GetMyActivityQuery"/>'s
/// handler. <c>AddSerginCore</c> registers them in every host, so the app bar's indicator gets an empty answer rather
/// than a missing handler where no module opted into scheduled tasks; <c>AddSerginScheduledTasks</c> calls it too.
/// Both use TryAdd, so whichever runs first registers them once.
/// </summary>
internal static class ActivityServiceCollectionExtensions
{
    public static IServiceCollection AddSerginRunActivity(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The sources' registrations are read when the directory is first built, after the host has added every one, so
        // a source registered later (another opted-in context, a test's) is still among them; a host with none reads none.
        services.TryAddSingleton<IBackgroundRunDirectory>(provider => new BackgroundRunDirectory(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<BackgroundRunDirectory>>(),
            [.. services.Where(descriptor => descriptor.ServiceType == typeof(IBackgroundRunSource) && !descriptor.IsKeyedService)]));
        services.TryAddTransient<IRequestHandler<GetMyActivityQuery, ErrorOr<IReadOnlyList<ActivityItem>>>, GetMyActivityQueryHandler>();

        return services;
    }
}
