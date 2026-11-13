using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Pages;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// The sends behind a generated child section's row and bulk Remove: a command built from the root's id and the child's
/// id, sent at the root's version when the command is version-guarded.
/// </summary>
public static class GeneratedChildBulk
{
    /// <summary>Sends <paramref name="command"/> for one child, at <paramref name="version"/> only when the command is version-guarded.</summary>
    public static Task<ViewSendResult> SendAsync(ISerginDispatcher dispatcher, CommandView command, Guid rootId, Guid id, RowVersion? version)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(command);

        object request = (command.BuildFromIds ?? throw new InvalidOperationException(
            $"{command.Request.FullName} cannot be sent for a child: its constructor is not exactly (RootId, Id)."))(rootId, id);

        return command.RequiresVersion
            ? command.Invoker.SendVersionedAsync(dispatcher, request, version)
            : command.Invoker.SendAsync(dispatcher, request);
    }

    /// <summary>
    /// Removes each of <paramref name="ids"/> in turn. Every remove moves the root's version, so each send carries the
    /// version the one before it returned; a stale answer ends the run, since every later send would carry the same stale
    /// version. A child the validator refuses (one in use) is refused alone. Returns the outcome and the last version seen.
    /// </summary>
    public static async Task<(SerginBulkOutcome Outcome, RowVersion? Version)> RemoveAsync(
        ISerginDispatcher dispatcher, CommandView command, Guid rootId, RowVersion? version, IReadOnlyList<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(ids);

        RowVersion? current = version;

        async Task<ErrorOr<Success>> SendOneAsync(Guid id)
        {
            ViewSendResult result = await SendAsync(dispatcher, command, rootId, id, current);

            if (result.IsError)
            {
                List<Error> errors = [.. result.Errors];
                return errors;
            }

            current = result.Version ?? current;

            return Result.Success;
        }

        SerginBulkOutcome outcome = await SerginBulk.RunAsync(ids, SendOneAsync, VersionErrors.IsStale);

        return (outcome, current);
    }
}
