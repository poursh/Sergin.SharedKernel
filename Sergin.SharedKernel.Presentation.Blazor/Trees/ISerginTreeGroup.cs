using Sergin.SharedKernel.Presentation.Blazor.Pages;

namespace Sergin.SharedKernel.Presentation.Blazor.Trees;

/// <summary>
/// What a <see cref="SerginTree"/> needs from one of its groups, whatever the group's node type.
/// <see cref="SerginTreeGroup{TNode}"/> is the one implementation.
/// </summary>
internal interface ISerginTreeGroup
{
    string Key { get; }

    string Title { get; }

    /// <summary>The add action of the group: the group header's <c>+</c>, or the static row's on a one-group page.</summary>
    SerginPageAction? AddAction { get; }

    /// <summary>Whether the roots are in; until then the group reads the requested node itself.</summary>
    bool Loaded { get; }

    /// <summary>The href of a loaded node, for a step of the selected node's path in the page header; null if not loaded.</summary>
    string? HrefFor(string nodeId);

    /// <summary>The selected node of this group; null while it has none.</summary>
    SerginTreeSelectedNode? Selected { get; }

    /// <summary>Selects <paramref name="nodeId"/>, loading and expanding its ancestors; null clears. False if not found.</summary>
    Task<bool> ShowAsync(string? nodeId);

    /// <summary>Loads the group again, keeping what was expanded and selected where it still exists.</summary>
    Task ReloadAsync();

    void CollapseAll();

    /// <summary>Shows only the loaded nodes whose label contains <paramref name="term"/>, and their ancestors.</summary>
    void Filter(string? term);

    /// <summary>Re-renders the group after the page changed something the group shows.</summary>
    void Refresh();
}
