using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>What became of a child form's submit.</summary>
public enum ChildSubmitStatus
{
    /// <summary>The command ran.</summary>
    Sent,

    /// <summary>The command answered with errors; see <see cref="ChildSubmitOutcome.Stale"/>.</summary>
    Refused,

    /// <summary>The command is version-guarded and the view holds no root version: nothing was sent.</summary>
    NoVersion,
}

/// <summary>A child form's outcome: status, the errors of a refusal, whether one was a stale version, and the id a create answered with.</summary>
public sealed record ChildSubmitOutcome(ChildSubmitStatus Status, IReadOnlyList<Error> Errors, bool Stale, Guid? CreatedId);

/// <summary>
/// The decisions behind a generated child form view, apart from the view: the command the form's values build (the one
/// the field validation reads and the submit sends), the submit at the root's version, and where the form leaves for.
/// </summary>
public static class GeneratedChildForm
{
    /// <summary>The add command (no <paramref name="id"/>) or the edit command of <paramref name="id"/>, built from <paramref name="values"/> under <paramref name="rootId"/>.</summary>
    public static object Build(ChildViewModel child, Guid rootId, Guid? id, IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(values);

        return id is { } target
            ? (child.Edit ?? throw new InvalidOperationException($"{child.Child.FullName} has no edit command.")).Build(rootId, target, values)
            : (child.Add ?? throw new InvalidOperationException($"{child.Child.FullName} has no add command.")).Build(rootId, values);
    }

    /// <summary>Whether the form's command is sent at the root's version.</summary>
    public static bool RequiresVersion(ChildViewModel child, bool isEdit)
    {
        ArgumentNullException.ThrowIfNull(child);

        Type? request = isEdit ? child.Edit?.Type : child.Add?.Type;

        return child.Commands.Any(command => command.Request == request && command.RequiresVersion);
    }

    /// <summary>
    /// Sends the form's command at <paramref name="version"/> when it is version-guarded; a guarded command with no
    /// version is not sent at all (the view reports not found and reads again, never sends a fabricated version).
    /// </summary>
    public static async Task<ChildSubmitOutcome> SubmitAsync(
        ISerginDispatcher dispatcher, ChildViewModel child, Guid rootId, Guid? id, IReadOnlyDictionary<string, object?> values, RowVersion? version)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(child);

        bool isEdit = id is not null;
        bool versioned = RequiresVersion(child, isEdit);

        if (versioned && version is null)
        {
            return new ChildSubmitOutcome(ChildSubmitStatus.NoVersion, [], false, null);
        }

        RequestInvoker invoker = (isEdit ? child.Edit?.Invoker : child.Add?.Invoker)
            ?? throw new InvalidOperationException($"{child.Child.FullName} has no {(isEdit ? "edit" : "add")} command.");
        object request = Build(child, rootId, id, values);

        ViewSendResult result = versioned
            ? await invoker.SendVersionedAsync(dispatcher, request, version)
            : await invoker.SendAsync(dispatcher, request);

        return result.IsError
            ? new ChildSubmitOutcome(ChildSubmitStatus.Refused, [.. result.Errors], result.Errors.Any(VersionErrors.IsStale), null)
            : new ChildSubmitOutcome(ChildSubmitStatus.Sent, [], false, (result.Value as ICreatedResponse)?.Id);
    }

    /// <summary>
    /// Where the form leaves for: an edit, the child's detail page; an add, the new child's detail page when the child has a
    /// detail route and the response named it, else the root's.
    /// </summary>
    public static string LeaveTarget(AggregateViewModel model, ChildViewModel child, Guid rootId, Guid? id, Guid? createdId)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(child);

        if (child.Routes.Detail is { } detail && (id ?? createdId) is { } childId)
        {
            return detail(rootId, childId);
        }

        return RootHref(model, rootId);
    }

    /// <summary>The root's detail page: where an add form's Cancel goes.</summary>
    public static string RootHref(AggregateViewModel model, Guid rootId)
    {
        ArgumentNullException.ThrowIfNull(model);

        return (model.Routes.Detail ?? throw new InvalidOperationException($"{model.Root.FullName} has no detail route.")).Invoke(rootId);
    }
}
