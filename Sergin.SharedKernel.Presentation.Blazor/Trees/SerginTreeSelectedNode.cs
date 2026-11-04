using Sergin.SharedKernel.Presentation.Blazor.Pages;

namespace Sergin.SharedKernel.Presentation.Blazor.Trees;

/// <summary>One step of the selected node's path in the page header.</summary>
public sealed record SerginTreePathStep(string Id, string Label);

/// <summary>
/// What a <see cref="SerginTree"/> shows about the selected node outside the group that owns it: the path in the page
/// header's trail, and the panel's dynamic actions row.
/// </summary>
/// <param name="Id">The node's id in its group.</param>
/// <param name="Label">The node's label.</param>
/// <param name="Ancestors">The steps from the root down to the node's parent.</param>
/// <param name="DynamicActions">The actions for the node's kind, shown in the panel's second row.</param>
public sealed record SerginTreeSelectedNode(
    string Id,
    string Label,
    IReadOnlyList<SerginTreePathStep> Ancestors,
    IReadOnlyList<SerginPageAction> DynamicActions);
