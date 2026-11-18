using Sergin.SharedKernel.Presentation.Blazor.Navigation;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Runtime;

/// <summary>
/// What a generated tree page derives once from an aggregate's view model: its routes, the layout and group keys, the
/// trail's one step, the shown Delete command and the self-parented create action.
/// </summary>
/// <param name="Tree">The root's tree.</param>
/// <param name="Href">The list route: the tree's own page.</param>
/// <param name="NodeHref">The detail route of a node.</param>
/// <param name="LayoutKey">Names the tree's layout: the schema and the list route's last segment (<c>ua.scopes</c>).</param>
/// <param name="GroupKey">Names the tree's group: the root's lower-case name (<c>scope</c>).</param>
/// <param name="Trail">The tree's trail: one step, the root's title linking to the list route.</param>
/// <param name="Delete">The root's shown Delete command, or null.</param>
/// <param name="AddChild">The self-parented create command, or null.</param>
internal sealed record GeneratedTreeParts(
    TreeView Tree,
    string Href,
    Func<Guid, string> NodeHref,
    string LayoutKey,
    string GroupKey,
    IReadOnlyList<SerginBreadcrumb> Trail,
    CommandView? Delete,
    CommandView? AddChild)
{
    public static GeneratedTreeParts For(AggregateViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        TreeView tree = model.Tree
            ?? throw new InvalidOperationException($"{model.Root.FullName} declares no Tree<T>() / ITreeQuery, so it has no generated tree page.");
        string href = model.Routes.List
            ?? throw new InvalidOperationException($"{model.Root.FullName} declares a tree but no list route to serve it from.");
        Func<Guid, string> nodeHref = model.Routes.Detail
            ?? throw new InvalidOperationException($"{model.Root.FullName} declares a tree but no detail route for its nodes.");

        string lastSegment = href.TrimEnd('/').Split('/')[^1];

        return new GeneratedTreeParts(
            tree,
            href,
            nodeHref,
            $"{model.Schema}.{lastSegment}",
            model.Root.Name.ToLowerInvariant(),
            [new SerginBreadcrumb(model.Title, href)],
            model.Commands.FirstOrDefault(command => command.Role == CommandRole.Delete && !command.Hidden),
            model.ChildCreate);
    }

    /// <summary>
    /// The delete request for <paramref name="id"/>: built from the id alone, or, when its confirm asks for values, with
    /// each of them at its default (a tree's delete has no dialog to ask with).
    /// </summary>
    public static object DeleteRequest(CommandView delete, Guid id)
    {
        ArgumentNullException.ThrowIfNull(delete);

        if (delete.BuildFromId is { } fromId)
        {
            return fromId(id);
        }

        var values = delete.ConfirmFields.ToDictionary(
            field => field.Name,
            field => field.ValueType.IsValueType ? Activator.CreateInstance(field.ValueType) : null);

        return delete.BuildFromValues!(id, values);
    }
}
