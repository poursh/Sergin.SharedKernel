using Sergin.SharedKernel.Presentation.Blazor.Pages;

namespace Sergin.SharedKernel.Presentation.Blazor.Trees;

/// <summary>One step of the selected node's path in the tree header.</summary>
public sealed record SerginTreePathStep(string Id, string Label, string? Icon);

/// <summary>
/// What a <see cref="SerginTree"/> shows about the selected node outside the group that owns it: the tree header's
/// path and menu, and the panel's dynamic actions row.
/// </summary>
/// <param name="Id">The node's id in its group.</param>
/// <param name="Label">The node's label.</param>
/// <param name="Icon">The node's icon, if any.</param>
/// <param name="Ancestors">The steps from the root down to the node's parent.</param>
/// <param name="DynamicActions">The actions for the node's kind, shown in the panel's second row.</param>
/// <param name="Menu">The node's menu, the same as on its row in the tree.</param>
public sealed record SerginTreeSelectedNode(
    string Id,
    string Label,
    string? Icon,
    IReadOnlyList<SerginTreePathStep> Ancestors,
    IReadOnlyList<SerginPageAction> DynamicActions,
    IReadOnlyList<SerginPageAction> Menu);
