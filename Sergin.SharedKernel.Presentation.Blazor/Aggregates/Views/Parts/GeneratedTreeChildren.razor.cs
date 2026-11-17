using System.Collections;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Errors;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

/// <summary>
/// A tree root's children list on its detail view: every descendant of the node (<see cref="TreeDescendants"/>), indented by
/// depth, each linking to its own page, under the tree's children title; the tree's empty text when there are none. The tree is
/// read through the tree query when the node or the generation changes, and not otherwise. A failed read shows its problem in
/// place of the list, no snackbar.
/// </summary>
public sealed partial class GeneratedTreeChildren
{
    private const int IndentPixels = 24;
    private const int BasePixels = 16;

    private IReadOnlyList<(object Item, Guid Id, string Title, int Depth)> descendants = [];
    private SerginProblem? problem;
    private Guid loadedFor;
    private int loadedGeneration = -1;

    /// <summary>The root's tree.</summary>
    [Parameter]
    [EditorRequired]
    public TreeView Tree { get; set; } = default!;

    /// <summary>The node whose descendants are listed.</summary>
    [Parameter]
    [EditorRequired]
    public Guid NodeId { get; set; }

    /// <summary>A node's page.</summary>
    [Parameter]
    [EditorRequired]
    public Func<Guid, string> NodeHref { get; set; } = default!;

    /// <summary>The detail view's generation: a change reads the tree again.</summary>
    [Parameter]
    public int Generation { get; set; }

    [Inject]
    private ISerginDispatcher Dispatcher { get; set; } = default!;

    [Inject]
    private IUiErrorPresenter ErrorPresenter { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        if (NodeId == loadedFor && Generation == loadedGeneration)
        {
            return;
        }

        loadedFor = NodeId;
        loadedGeneration = Generation;

        ViewSendResult loaded = await Tree.Query.Invoker.SendAsync(Dispatcher, Tree.Query.Build());

        if (loaded.IsError)
        {
            problem = ErrorPresenter.Present(loaded.Errors[0]);
            descendants = [];

            return;
        }

        problem = null;
        IReadOnlyCollection<object> items = [.. ((IEnumerable)loaded.Value!).Cast<object>()];
        descendants = TreeDescendants.Of(Tree, items, NodeId);
    }

    private static string Indent(int depth) =>
        string.Create(CultureInfo.InvariantCulture, $"padding-left: {depth * IndentPixels + BasePixels}px");
}
