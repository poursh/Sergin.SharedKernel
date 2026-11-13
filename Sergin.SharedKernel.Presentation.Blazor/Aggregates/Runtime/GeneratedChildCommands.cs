using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>What became of a child command a detail view sent.</summary>
public enum ChildCommandStatus
{
    /// <summary>The command ran.</summary>
    Sent,

    /// <summary>The command answered with errors; see <see cref="ChildCommandOutcome.Stale"/>.</summary>
    Refused,

    /// <summary>The command is version-guarded and the view holds no version: nothing was sent.</summary>
    NoVersion,
}

/// <summary>A child command's outcome: its status, the errors of a refusal and whether one of them was a stale version.</summary>
public sealed record ChildCommandOutcome(ChildCommandStatus Status, IReadOnlyList<Error> Errors, bool Stale);

/// <summary>
/// The decision behind a generated child detail view's Remove and transitions, apart from the view: a guarded command
/// with no root version is not sent (the view reports not found and reads again, never sends a fabricated version), any
/// other goes out through <see cref="GeneratedChildBulk.SendAsync"/> at the version held.
/// </summary>
public static class GeneratedChildCommands
{
    /// <summary>Sends <paramref name="command"/> for one child unless it needs a version and <paramref name="version"/> is null.</summary>
    public static async Task<ChildCommandOutcome> SendAsync(
        ISerginDispatcher dispatcher, CommandView command, Guid rootId, Guid id, RowVersion? version)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.RequiresVersion && version is null)
        {
            return new ChildCommandOutcome(ChildCommandStatus.NoVersion, [], false);
        }

        ViewSendResult result = await GeneratedChildBulk.SendAsync(dispatcher, command, rootId, id, version);

        if (!result.IsError)
        {
            return new ChildCommandOutcome(ChildCommandStatus.Sent, [], false);
        }

        return new ChildCommandOutcome(ChildCommandStatus.Refused, [.. result.Errors], result.Errors.Any(VersionErrors.IsStale));
    }

    /// <summary>Where a view leaves for after a child is removed: the root's detail page.</summary>
    public static string LeaveTarget(AggregateViewModel model, Guid rootId)
    {
        ArgumentNullException.ThrowIfNull(model);

        return (model.Routes.Detail ?? throw new InvalidOperationException($"{model.Root.FullName} has no detail route to leave for.")).Invoke(rootId);
    }
}
