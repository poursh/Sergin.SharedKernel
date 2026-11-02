namespace Sergin.SharedKernel.Presentation.Blazor.Trees;

/// <summary>
/// One loaded node of a <see cref="SerginTreeGroup{TNode}"/>: the node, where it sits, and what the panel knows about
/// it — whether its children are loaded, loading, and shown. Created and changed only by the group; public because
/// <see cref="SerginTreeBranch{TNode}"/>, a component, takes it as a parameter.
/// </summary>
public sealed class SerginTreeNodeState<TNode>
    where TNode : notnull
{
    internal SerginTreeNodeState(TNode node, string id, SerginTreeNodeState<TNode>? parent)
    {
        Node = node;
        Id = id;
        Parent = parent;
        Level = parent is null ? 0 : parent.Level + 1;
    }

    public TNode Node { get; }

    public string Id { get; }

    public SerginTreeNodeState<TNode>? Parent { get; }

    public int Level { get; }

    /// <summary>The loaded children; null until they are first loaded.</summary>
    public IReadOnlyList<SerginTreeNodeState<TNode>>? Children { get; internal set; }

    public bool Expanded { get; internal set; }

    public bool Loading { get; internal set; }

    /// <summary>The node and its ancestors, nearest first.</summary>
    public IEnumerable<SerginTreeNodeState<TNode>> SelfAndAncestors()
    {
        for (SerginTreeNodeState<TNode>? current = this; current is not null; current = current.Parent)
        {
            yield return current;
        }
    }
}
