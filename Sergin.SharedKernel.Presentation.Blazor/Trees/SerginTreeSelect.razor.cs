using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Presentation.Blazor.Navigation;

namespace Sergin.SharedKernel.Presentation.Blazor.Trees;

/// <summary>
/// Names the node a page inside a tree layout shows: <c>&lt;SerginTreeSelect Node="@Id.ToString()" /&gt;</c> on a
/// detail page, <c>&lt;SerginTreeSelect Node="@ParentId?.ToString()" Tail="New scope" /&gt;</c> on a create page.
/// </summary>
/// <remarks>
/// The tree highlights the node, opens its ancestors and adds its path to the page header's trail. Navigating to another page
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

    /// <summary>Transitional: the trail's last step as a plain string, shown as written.</summary>
    [Parameter]
    public string? Tail { get; set; }

    /// <summary>What the page shows under the node, as the trail's last step; null on the node's page.</summary>
    [Parameter]
    public TextDefinition? TailText { get; set; }

    private SerginBreadcrumb? TailStep
    {
        get
        {
            if (TailText is { } text)
            {
                return new SerginBreadcrumb(text);
            }

            return Tail is { } value ? SerginBreadcrumb.Data(value) : null;
        }
    }

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
            Tree.Release(this, TailStep);
        }
        else
        {
            Tree.Select(this, Group, Node, TailStep);
        }
    }
}
