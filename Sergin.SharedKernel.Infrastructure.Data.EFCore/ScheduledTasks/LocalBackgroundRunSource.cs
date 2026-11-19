using ErrorOr;
using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Application.ScheduledTasks.Activity;
using Sergin.SharedKernel.Application.ScheduledTasks.Queries;
using Sergin.SharedKernel.Domain.Users;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.ScheduledTasks;

/// <summary>
/// The runs of one opted-in context in this process, read from its schema through <see cref="IScheduledTaskQueries"/>.
/// <c>AddModuleDbContext</c> registers one per opted-in context. A read the queries refuse is logged and answers no runs.
/// </summary>
internal sealed partial class LocalBackgroundRunSource(
    string schema, IScheduledTaskQueries queries, ILogger<LocalBackgroundRunSource> logger) : IBackgroundRunSource
{
    public string Schema => schema;

    public async Task<IReadOnlyList<ActivityItem>> ListActivityAsync(UserId user, DateTime finishedSinceUtc, int take, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);

        ErrorOr<IReadOnlyList<ActivityItem>> items = await queries.ActivityForUserAsync(schema, user.Value, finishedSinceUtc, take, ct);

        if (items.IsError)
        {
            LogReadRefused(schema, items.FirstError.Code);
            return [];
        }

        return items.Value;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The background runs of module {Schema} were not read: {ErrorCode}.")]
    private partial void LogReadRefused(string schema, string errorCode);
}
