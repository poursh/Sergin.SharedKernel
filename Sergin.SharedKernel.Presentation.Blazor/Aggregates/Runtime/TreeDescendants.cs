namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// Every descendant of a node in a root's tree, as the generated children list shows them: depth first, siblings by title in
/// the current culture, depth 0 for a direct child, 1 for a grandchild, and so on. Empty when the node has no children or is
/// not among the items. An item reached twice (a cycle in the data) is listed once.
/// </summary>
internal static class TreeDescendants
{
    public static IReadOnlyList<(object Item, Guid Id, string Title, int Depth)> Of(TreeView tree, IReadOnlyCollection<object> items, Guid nodeId)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(items);

        ILookup<Guid?, (object Item, Guid Id, string Title)> byParent = items
            .Select(item => (Item: item, Id: tree.IdOf(item), Title: tree.TitleOf(item)))
            .ToLookup(node => tree.ParentIdOf(node.Item));

        List<(object Item, Guid Id, string Title, int Depth)> result = [];
        HashSet<Guid> visited = [nodeId];
        Stack<((object Item, Guid Id, string Title) Node, int Depth)> pending = new();

        Push(pending, byParent[nodeId], 0);

        while (pending.Count > 0)
        {
            ((object item, Guid id, string title), int depth) = pending.Pop();

            if (!visited.Add(id))
            {
                continue;
            }

            result.Add((item, id, title, depth));
            Push(pending, byParent[id], depth + 1);
        }

        return result;
    }

    // Pushed in reverse title order, so the stack pops them in title order.
    private static void Push(
        Stack<((object Item, Guid Id, string Title) Node, int Depth)> pending, IEnumerable<(object Item, Guid Id, string Title)> children, int depth)
    {
        foreach ((object Item, Guid Id, string Title) child in children.OrderBy(child => child.Title, StringComparer.CurrentCulture).Reverse())
        {
            pending.Push((child, depth));
        }
    }
}
