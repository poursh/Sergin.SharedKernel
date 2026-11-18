using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Activity;

/// <summary>
/// Asks every <see cref="IBackgroundRunSource"/> at once, each in a scope of its own (a source's context is scoped, and
/// two reads in parallel cannot share one), and merges what they answer, newest first. A source that throws is logged
/// and skipped, so one module's broken store never hides the others' runs. A singleton: it holds only the root scope
/// factory.
/// </summary>
internal sealed partial class BackgroundRunDirectory(IServiceScopeFactory scopes, ILogger<BackgroundRunDirectory> logger) : IBackgroundRunDirectory
{
    /// <summary>The most runs one source answers with.</summary>
    internal const int TakePerSource = 50;

    public async Task<IReadOnlyList<ActivityItem>> ListActivityAsync(UserId user, DateTime finishedSinceUtc, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);

        int count;

        await using (AsyncServiceScope probe = scopes.CreateAsyncScope())
        {
            count = probe.ServiceProvider.GetServices<IBackgroundRunSource>().Count();
        }

        IReadOnlyList<ActivityItem>[] answers =
            await Task.WhenAll(Enumerable.Range(0, count).Select(index => ListOneAsync(index, user, finishedSinceUtc, ct)));

        return
        [
            .. answers
                .SelectMany(items => items)
                .OrderByDescending(item => item.StartedAtUtc)
                .ThenByDescending(item => item.Ref.RunId),
        ];
    }

    /// <summary>The source at <paramref name="index"/>, resolved in its own scope: sources are told apart only by their order.</summary>
    private async Task<IReadOnlyList<ActivityItem>> ListOneAsync(int index, UserId user, DateTime finishedSinceUtc, CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        IBackgroundRunSource source = scope.ServiceProvider.GetServices<IBackgroundRunSource>().ElementAt(index);

        try
        {
            return await source.ListActivityAsync(user, finishedSinceUtc, TakePerSource, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            LogSourceFailed(exception, source.Schema);
            return [];
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The background runs of module {Schema} could not be read; they are left out of the activity.")]
    private partial void LogSourceFailed(Exception exception, string schema);
}
