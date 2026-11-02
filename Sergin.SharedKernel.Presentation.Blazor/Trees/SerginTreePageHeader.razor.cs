using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Trees;

/// <summary>
/// The tree header of a <see cref="SerginTree"/>: the selected node's path (80%) and its menu items (20%).
/// </summary>
public sealed partial class SerginTreePageHeader
{
    /// <summary>The active group's title, shown first when the page has several groups; null otherwise.</summary>
    [Parameter]
    public string? GroupTitle { get; set; }

    [Parameter]
    public SerginTreeSelectedNode? Node { get; set; }

    /// <summary>
    /// What the page shows under the node, as the path's last step (localised) — "New scope" on a create page; the node
    /// is then a link. Null on the node's own page.
    /// </summary>
    [Parameter]
    public string? Tail { get; set; }

    /// <summary>The section the pane view's header buttons render into.</summary>
    [Parameter]
    [EditorRequired]
    public object HeaderSectionId { get; set; } = default!;

    /// <summary>An ancestor's id, when the user picks it from the path.</summary>
    [Parameter]
    public EventCallback<string> OnAncestor { get; set; }

    /// <summary>The group title was picked: clears the selection.</summary>
    [Parameter]
    public EventCallback OnGroup { get; set; }

    /// <summary>The close button was picked: goes to the tree's own route.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;
}
