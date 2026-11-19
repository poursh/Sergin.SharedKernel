using Sergin.SharedKernel.Application.Commands.Configuration;
using Sergin.SharedKernel.Application.Commands.Queries;
using Sergin.SharedKernel.Application.Securities.Users;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Activity;

/// <summary>The caller's own runs across modules: running, or finished since <paramref name="FinishedSinceUtc"/>.</summary>
public sealed record GetMyActivityQuery(DateTime FinishedSinceUtc) : IQuery<IReadOnlyList<ActivityItem>>;

internal sealed class GetMyActivityQueryHandler(IBackgroundRunDirectory directory, IUserContext caller)
    : IQueryHandler<GetMyActivityQuery, IReadOnlyList<ActivityItem>>
{
    public async Task<ErrorOr<IReadOnlyList<ActivityItem>>> Handle(GetMyActivityQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyList<ActivityItem> items = await directory.ListActivityAsync(caller.Id, request.FinishedSinceUtc, cancellationToken);

        return items.ToErrorOr();
    }
}

// Untyped, so checked on the current scope, with no permission to check there: a person's own runs are theirs to see
// whatever they may do now. The window is set by the caller, never shown.
internal sealed class GetMyActivityQueryConfiguration : ICommandConfiguration<GetMyActivityQuery>
{
    public void Configure(CommandConfigurationBuilder<GetMyActivityQuery> builder) => builder.NotShown(x => x.FinishedSinceUtc);
}
