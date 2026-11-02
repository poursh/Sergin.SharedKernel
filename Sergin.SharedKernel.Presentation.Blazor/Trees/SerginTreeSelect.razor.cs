using Microsoft.AspNetCore.Components;

namespace Sergin.SharedKernel.Presentation.Blazor.Trees;

/// <summary>
/// Names the node a page inside a tree layout shows: <c>&lt;SerginTreeSelect Node="@Id.ToString()" /&gt;</c> on a
/// detail page, <c>&lt;SerginTreeSelect Node="@ParentId?.ToString()" Tail="New scope" /&gt;</c> on a create page.
/// </summary>
/// <remarks>
/// The tree highlights the node, opens its ancestors and draws its path in the tree header. Navigating to another page
/// in the same layout hands the selection to that page's marker; the old marker, disposed afterwards, clears it only if
/// it still owns it. Outside a <see cref="SerginTree"/> it does nothing, so the page still renders.
/// </remarks>
public sealed partial class SerginTreeSelect : IDisposable
{
    /// <summary>The node's id in its group; null or blank selects nothing.</summary>
    [Parameter]
    [EditorRequired]
    public string? Node { get; set; }

    /// <summary>The node's group key, with several groups; null means the active group.</summary>
    [Parameter]
    public string? Group { get; set; }

    /// <summary>What the page shows under the node, as the tree header's last step (localised); null on the node's page.</summary>
    [Parameter]
    public string? Tail { get; set; }

    [CascadingParameter]
    private SerginTree? Tree { get; set; }

    public void Dispose() => Tree?.Release(this);

    protected override void OnParametersSet()
    {
        if (Tree is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Node))
        {
            Tree.Release(this, Tail);
        }
        else
        {
            Tree.Select(this, Group, Node, Tail);
        }
    }
}
