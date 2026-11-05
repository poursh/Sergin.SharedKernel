using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Sergin.SharedKernel.Presentation.Blazor.Trees;

/// <summary>One level of a <see cref="SerginTreeGroup{TNode}"/>'s tree; renders the next level inside each expanded row.</summary>
public sealed partial class SerginTreeBranch<TNode>
    where TNode : notnull
{
    private const int IndentPerLevel = 16;

    [Parameter]
    [EditorRequired]
    public SerginTreeGroup<TNode> Group { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<SerginTreeNodeState<TNode>> Nodes { get; set; } = [];

    [Parameter]
    public int Level { get; set; }

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    private IEnumerable<SerginTreeNodeState<TNode>> VisibleNodes => Nodes.Where(Group.IsVisible);

    // Null leaves the attribute out: a leaf is neither expanded nor collapsed.
    private string? AriaExpanded(SerginTreeNodeState<TNode> state)
    {
        if (!Group.CanExpand(state))
        {
            return null;
        }

        return Group.IsExpanded(state) ? "true" : "false";
    }

    private static string Indent(SerginTreeNodeState<TNode> state, int extra = 0) =>
        string.Create(CultureInfo.InvariantCulture, $"padding-left: {(state.Level + extra) * IndentPerLevel}px");
}
