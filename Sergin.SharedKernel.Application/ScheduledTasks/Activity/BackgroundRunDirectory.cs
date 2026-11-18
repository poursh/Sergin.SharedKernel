using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Activity;

/// <summary>
/// Asks every <see cref="IBackgroundRunSource"/> at once, each in a scope of its own (a source's context is scoped, and
/// two reads in parallel cannot share one), and merges what they answer, newest first. A source that throws, whether
/// while it is built or while it reads, is logged and skipped, so one module's broken store never hides the others'
/// runs. A singleton: it holds only the root scope factory and the sources' registrations.
/// </summary>
/// <param name="registrations">
/// Every non-keyed <see cref="IBackgroundRunSource"/> registration. Each source is built from its own registration, not
/// through <c>GetServices</c>, which builds them all at once and so lets one failing constructor fail every source.
/// </param>
internal sealed partial class BackgroundRunDirectory(
    IServiceScopeFactory scopes, ILogger<BackgroundRunDirectory> logger, IReadOnlyList<ServiceDescriptor> registrations) : IBackgroundRunDirectory
{
    /// <summary>The most runs one source answers with.</summary>
    internal const int TakePerSource = 50;

    public async Task<IReadOnlyList<ActivityItem>> ListActivityAsync(UserId user, DateTime finishedSinceUtc, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);

        IReadOnlyList<ActivityItem>[] answers =
            await Task.WhenAll(registrations.Select((registration, index) => ListOneAsync(registration, index, user, finishedSinceUtc, ct)));

        return
        [
            .. answers
                .SelectMany(items => items)
                .OrderByDescending(item => item.StartedAtUtc)
                .ThenByDescending(item => item.Ref.RunId),
        ];
    }

    private async Task<IReadOnlyList<ActivityItem>> ListOneAsync(
        ServiceDescriptor registration, int index, UserId user, DateTime finishedSinceUtc, CancellationToken ct)
    {
        string? schema = null;

        try
        {
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();
            IBackgroundRunSource source = Build(registration, scope.ServiceProvider);
            schema = source.Schema;

            return await source.ListActivityAsync(user, finishedSinceUtc, TakePerSource, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            LogSourceFailed(exception, schema ?? $"#{index}");
            return [];
        }
    }

    // As the container would build it; the scope disposes what it resolves, and a source holds nothing to dispose.
    private static IBackgroundRunSource Build(ServiceDescriptor registration, IServiceProvider services) =>
        (IBackgroundRunSource)(registration.ImplementationInstance
            ?? registration.ImplementationFactory?.Invoke(services)
            ?? ActivatorUtilities.CreateInstance(services, registration.ImplementationType!));

    [LoggerMessage(Level = LogLevel.Warning, Message = "The background runs of source {Source} could not be read; they are left out of the activity.")]
    private partial void LogSourceFailed(Exception exception, string source);
}
