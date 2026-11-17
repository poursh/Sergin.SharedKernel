namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// The parents a record may be moved under, as the Move dialog offers them: every item of the root's tree except the
/// record itself and its descendants (a move under either would make a cycle; the server refusing one stays the
/// guarantee), depth first, siblings by title in the current culture. An item whose parent is not among the items (a
/// caller who reads only part of the tree) is a top-level item; a top-level item's depth is 0.
/// </summary>
internal static class MoveCandidates
{
    public static IReadOnlyList<(Guid Id, string Title, int Depth)> For(TreeView tree, IReadOnlyCollection<object> items, Guid id)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(items);

        (Guid Id, Guid? ParentId, string Title)[] nodes = [.. items.Select(item => (tree.IdOf(item), tree.ParentIdOf(item), tree.TitleOf(item)))];
        HashSet<Guid> known = [.. nodes.Select(node => node.Id)];
        ILookup<Guid?, (Guid Id, Guid? ParentId, string Title)> byParent = nodes.ToLookup(
            node => node.ParentId is { } parent && known.Contains(parent) ? node.ParentId : null);

        List<(Guid Id, string Title, int Depth)> candidates = [];
        HashSet<Guid> visited = [];
        Stack<((Guid Id, Guid? ParentId, string Title) Node, int Depth)> pending = new();

        PushChildren(pending, byParent[null], 0);

        while (pending.Count > 0)
        {
            ((Guid Id, Guid? ParentId, string Title) node, int depth) = pending.Pop();

            // The record and everything below it are skipped, as is any item reached twice (a cycle in the data).
            if (node.Id == id || !visited.Add(node.Id))
            {
                continue;
            }

            candidates.Add((node.Id, node.Title, depth));
            PushChildren(pending, byParent[node.Id], depth + 1);
        }

        return candidates;
    }

    // Pushed in reverse title order, so the stack pops them in title order (ties as they came).
    private static void PushChildren(
        Stack<((Guid Id, Guid? ParentId, string Title) Node, int Depth)> pending, IEnumerable<(Guid Id, Guid? ParentId, string Title)> children, int depth)
    {
        foreach ((Guid Id, Guid? ParentId, string Title) child in children.OrderBy(child => child.Title, StringComparer.CurrentCulture).Reverse())
        {
            pending.Push((child, depth));
        }
    }
}
