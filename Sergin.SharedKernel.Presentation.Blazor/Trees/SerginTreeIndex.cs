namespace Sergin.SharedKernel.Presentation.Blazor.Trees;

/// <summary>
/// A flat list of self-referencing nodes as a tree: each node under its parent, siblings by label. What
/// <see cref="SerginAggregateTree{TNode}"/> answers its tree group's loaders from.
/// </summary>
/// <remarks>
/// A node whose parent is not in the list is a root — the caller may read only part of the tree, so a scope whose
/// parent it cannot reach still shows, at the top. A parent chain that loops is cut where it repeats.
/// </remarks>
public sealed class SerginTreeIndex<TNode>
    where TNode : notnull
{
    private readonly Dictionary<string, TNode> byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> parentOf = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<TNode>> childrenOf = new(StringComparer.Ordinal);

    public SerginTreeIndex(IEnumerable<TNode> nodes, Func<TNode, string> id, Func<TNode, string?> parentId, Func<TNode, string> label)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(parentId);
        ArgumentNullException.ThrowIfNull(label);

        foreach (TNode node in nodes)
        {
            byId.TryAdd(id(node), node);
        }

        List<TNode> roots = [];
        Dictionary<string, List<TNode>> children = new(StringComparer.Ordinal);

        foreach ((string nodeId, TNode node) in byId)
        {
            string? parent = parentId(node);

            if (parent is not null && !string.Equals(parent, nodeId, StringComparison.Ordinal) && byId.ContainsKey(parent))
            {
                parentOf[nodeId] = parent;

                if (!children.TryGetValue(parent, out List<TNode>? siblings))
                {
                    siblings = [];
                    children[parent] = siblings;
                }

                siblings.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        StringComparer order = StringComparer.CurrentCulture;
        Roots = [.. roots.OrderBy(label, order)];

        foreach ((string parent, List<TNode> siblings) in children)
        {
            childrenOf[parent] = [.. siblings.OrderBy(label, order)];
        }
    }

    public IReadOnlyList<TNode> Roots { get; }

    public int Count => byId.Count;

    public TNode? Find(string nodeId) => byId.TryGetValue(nodeId, out TNode? node) ? node : default;

    public IReadOnlyList<TNode> ChildrenOf(string nodeId) => childrenOf.TryGetValue(nodeId, out IReadOnlyList<TNode>? children) ? children : [];

    public int ChildCount(string nodeId) => ChildrenOf(nodeId).Count;

    /// <summary>The nodes from a root down to <paramref name="nodeId"/>, both included; empty for an unknown id.</summary>
    public IReadOnlyList<TNode> PathTo(string nodeId)
    {
        if (!byId.TryGetValue(nodeId, out TNode? node))
        {
            return [];
        }

        List<TNode> path = [node];
        HashSet<string> seen = new(StringComparer.Ordinal) { nodeId };
        string current = nodeId;

        while (parentOf.TryGetValue(current, out string? parent) && seen.Add(parent))
        {
            path.Add(byId[parent]);
            current = parent;
        }

        path.Reverse();

        return path;
    }
}
