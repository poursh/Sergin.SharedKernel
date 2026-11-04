using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Pages;
using Sergin.SharedKernel.Presentation.Errors;

namespace Sergin.SharedKernel.Presentation.Blazor.Trees;

/// <summary>
/// One data source of a <see cref="SerginTree"/>, with its own node type, loaders and menus:
/// <c>&lt;SerginTreeGroup TNode="ScopeTreeItem" Key="scope" Title="Scopes" Roots="…" Children="…" … /&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// Roots load when the group is first rendered, children when a node is first expanded, each level showing a skeleton
/// meanwhile; <see cref="Eager"/> loads every level up front instead, which is what makes the filter see the whole
/// tree. <see cref="PathTo"/> lets a node a page selects load and expand the ancestors of a node not yet loaded.
/// </para>
/// <para>
/// A label click navigates to the node's page (<see cref="NodeHref"/>), which selects it; the chevron only expands; the
/// ⋮ menu (<see cref="NodeMenu"/>) works on any node and leaves the selection alone. Keyboard on a focused row: arrows
/// move and expand, Enter opens, Esc goes to the tree's own route — on a row only, so Esc closing a ⋮ menu keeps it.
/// </para>
/// </remarks>
public sealed partial class SerginTreeGroup<TNode> : ISerginTreeGroup, IDisposable
    where TNode : notnull
{
    private readonly HashSet<string> expandedIds = new(StringComparer.Ordinal);
    private List<SerginTreeNodeState<TNode>>? roots;
    private SerginProblem? problem;
    private SerginTreeNodeState<TNode>? selected;
    private string? cursorId;
    private string? filterTerm;
    private HashSet<string> visibleIds = new(StringComparer.Ordinal);
    private int matchCount;

    /// <summary>The group's key, which a <see cref="SerginTreeSelect"/> names with several groups; unique in the tree.</summary>
    [Parameter]
    [EditorRequired]
    public string Key { get; set; } = string.Empty;

    /// <summary>The group header's title and, with several groups, the page header's step before the selected node's path (localised).</summary>
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    [Parameter]
    public string? Icon { get; set; }

    [Parameter]
    [EditorRequired]
    public Func<CancellationToken, Task<ErrorOr<IReadOnlyCollection<TNode>>>> Roots { get; set; } = default!;

    /// <summary>Loads a node's children; null for a flat group whose roots are leaves.</summary>
    [Parameter]
    public Func<TNode, CancellationToken, Task<ErrorOr<IReadOnlyCollection<TNode>>>>? Children { get; set; }

    /// <summary>
    /// The nodes from a root down to an id, both included — how a node selected from the URL is found before its
    /// branch is loaded. Null: only loaded nodes can be selected that way.
    /// </summary>
    [Parameter]
    public Func<string, CancellationToken, Task<ErrorOr<IReadOnlyList<TNode>>>>? PathTo { get; set; }

    [Parameter]
    [EditorRequired]
    public Func<TNode, string> NodeId { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public Func<TNode, string> NodeLabel { get; set; } = default!;

    /// <summary>The route of the node's page, where a label click or Enter goes: <c>node => $"/ua/scopes/{node.Id}"</c>.</summary>
    [Parameter]
    [EditorRequired]
    public Func<TNode, string> NodeHref { get; set; } = default!;

    [Parameter]
    public Func<TNode, string?>? NodeIcon { get; set; }

    /// <summary>The figure after a node's label — its children, its devices; null shows none.</summary>
    [Parameter]
    public Func<TNode, int?>? NodeCount { get; set; }

    /// <summary>Whether a node can expand before its children are loaded; default: whenever <see cref="Children"/> is set.</summary>
    [Parameter]
    public Func<TNode, bool>? HasChildren { get; set; }

    /// <summary>Replaces the label text of a row; the icon, count and menu stay.</summary>
    [Parameter]
    public RenderFragment<TNode>? NodeTemplate { get; set; }

    /// <summary>Loads every level when the roots load, so the filter sees the whole tree.</summary>
    [Parameter]
    public bool Eager { get; set; }

    /// <summary>The group's add action: its header's <c>+</c>, or the static row's on a one-group page.</summary>
    [Parameter]
    public SerginPageAction? AddAction { get; set; }

    /// <summary>The actions for the selected node's kind, in the panel's second row.</summary>
    [Parameter]
    public Func<TNode, IReadOnlyList<SerginPageAction>>? DynamicActions { get; set; }

    /// <summary>A node's ⋮ menu, on its row.</summary>
    [Parameter]
    public Func<TNode, IReadOnlyList<SerginPageAction>>? NodeMenu { get; set; }

    /// <summary>The selected node's content — normally its detail view, which the page hosts as a pane.</summary>

    /// <summary>Replaces the empty state shown when the group has no roots.</summary>
    [Parameter]
    public RenderFragment? EmptyContent { get; set; }

    [Parameter]
    public string EmptyText { get; set; } = "Nothing here yet.";

    [CascadingParameter]
    private SerginTree? CascadedPage { get; set; }

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    [Inject]
    private IUiErrorPresenter ErrorPresenter { get; set; } = default!;

    /// <summary>The row buttons, so the keyboard can move focus between them.</summary>
    internal Dictionary<string, ElementReference> RowRefs { get; } = new(StringComparer.Ordinal);

    internal SerginTree Page { get; private set; } = default!;

    private bool Active => Page.IsActive(this);

    private IReadOnlyList<SerginPageAction> EmptyActions =>
        AddAction is null ? [] : [AddAction with { Kind = SerginActionKind.Primary }];

    private string MatchSummary => matchCount == 1
        ? Localizer["1 match"].Value
        : string.Format(CultureInfo.CurrentCulture, Localizer["{0} matches"].Value, matchCount);

    SerginTreeSelectedNode? ISerginTreeGroup.Selected => selected is null ? null : Describe(selected);

    bool ISerginTreeGroup.Loaded => roots is not null;

    string? ISerginTreeGroup.HrefFor(string nodeId) => Find(nodeId) is { } state ? NodeHref(state.Node) : null;

    public void Dispose() => Page?.Unregister(this);

    /// <summary>Loads the group again; what was expanded stays open, and the node the page shows now is selected.</summary>
    public async Task ReloadAsync()
    {
        await LoadRootsAsync();

        // The page's node as of now, not as of the start: the user may have moved on while the roots loaded.
        if (Page.RequestedNodeFor(this) is { } nodeId && !await ShowAsync(nodeId))
        {
            Page.SelectionGone(this);
        }

        StateHasChanged();
    }

    public async Task<bool> ShowAsync(string? nodeId)
    {
        if (nodeId is null)
        {
            selected = null;

            return true;
        }

        SerginTreeNodeState<TNode>? found = Find(nodeId);

        if (found is null)
        {
            found = await LoadPathAsync(nodeId);

            // A later page asked for another node while the path loaded: that request wins.
            if (!string.Equals(nodeId, Page.RequestedNodeFor(this), StringComparison.Ordinal))
            {
                return true;
            }
        }

        selected = found;

        if (found is null)
        {
            return false;
        }

        cursorId = found.Id;

        foreach (SerginTreeNodeState<TNode> ancestor in found.SelfAndAncestors().Skip(1))
        {
            ancestor.Expanded = true;
            expandedIds.Add(ancestor.Id);
        }

        return true;
    }

    public void CollapseAll()
    {
        foreach (SerginTreeNodeState<TNode> state in All())
        {
            state.Expanded = false;
        }

        expandedIds.Clear();
        StateHasChanged();
    }

    public void Filter(string? term)
    {
        filterTerm = string.IsNullOrWhiteSpace(term) ? null : term.Trim();
        visibleIds = new HashSet<string>(StringComparer.Ordinal);
        matchCount = 0;

        if (filterTerm is not null)
        {
            foreach (SerginTreeNodeState<TNode> state in All().Where(state => NodeLabel(state.Node).Contains(filterTerm, StringComparison.CurrentCultureIgnoreCase)))
            {
                matchCount++;
                visibleIds.UnionWith(state.SelfAndAncestors().Select(step => step.Id));
            }
        }

        StateHasChanged();
    }

    public void Refresh() => StateHasChanged();

    internal bool IsVisible(SerginTreeNodeState<TNode> state) => filterTerm is null || visibleIds.Contains(state.Id);

    // A filter shows every branch holding a match open, whatever the user expanded.
    internal bool IsExpanded(SerginTreeNodeState<TNode> state) =>
        state.Expanded || filterTerm is not null && state.Children is { } children && children.Any(child => visibleIds.Contains(child.Id));

    internal bool CanExpand(SerginTreeNodeState<TNode> state) =>
        state.Children is { } children ? children.Count > 0 : HasChildren?.Invoke(state.Node) ?? Children is not null;

    internal bool IsSelected(SerginTreeNodeState<TNode> state) => ReferenceEquals(state, selected);

    // One row is in the tab order: the cursor, or the first root until the user moves.
    internal bool IsCursor(SerginTreeNodeState<TNode> state) =>
        cursorId is null ? roots is { Count: > 0 } && ReferenceEquals(roots[0], state) : string.Equals(cursorId, state.Id, StringComparison.Ordinal);

    internal string LabelOf(SerginTreeNodeState<TNode> state) => NodeLabel(state.Node);

    internal string? IconOf(SerginTreeNodeState<TNode> state) => NodeIcon?.Invoke(state.Node);

    internal int? CountOf(SerginTreeNodeState<TNode> state) => NodeCount?.Invoke(state.Node);

    internal IReadOnlyList<SerginPageAction> MenuFor(SerginTreeNodeState<TNode> state) => NodeMenu?.Invoke(state.Node) ?? [];

    internal Task SelectAsync(SerginTreeNodeState<TNode> state)
    {
        cursorId = state.Id;
        Page.Navigate(NodeHref(state.Node));

        return Task.CompletedTask;
    }

    internal async Task ToggleAsync(SerginTreeNodeState<TNode> state)
    {
        if (state.Expanded)
        {
            state.Expanded = false;
            expandedIds.Remove(state.Id);

            return;
        }

        state.Expanded = true;
        expandedIds.Add(state.Id);

        if (state.Children is null)
        {
            await LoadChildrenAsync(state);
        }
    }

    internal async Task OnKeyDownAsync(SerginTreeNodeState<TNode> state, KeyboardEventArgs args)
    {
        cursorId = state.Id;

        switch (args.Key)
        {
            case "ArrowRight" when CanExpand(state) && !IsExpanded(state):
                await ToggleAsync(state);
                break;
            case "ArrowRight" when IsExpanded(state) && state.Children is { Count: > 0 } children:
                await FocusAsync(children.FirstOrDefault(IsVisible));
                break;
            case "ArrowLeft" when IsExpanded(state):
                await ToggleAsync(state);
                break;
            case "ArrowLeft":
                await FocusAsync(state.Parent);
                break;
            case "ArrowDown":
                await FocusAsync(Neighbour(state, 1));
                break;
            case "ArrowUp":
                await FocusAsync(Neighbour(state, -1));
                break;
            case "Escape":
                await Page.ClearSelectionAsync();
                break;
            default:
                break;
        }
    }

    protected override void OnInitialized()
    {
        Page = CascadedPage ?? throw new InvalidOperationException($"A SerginTreeGroup ('{Key}') must be placed inside a SerginTree.");

        if (string.IsNullOrWhiteSpace(Key))
        {
            throw new InvalidOperationException("A SerginTreeGroup needs a non-blank key.");
        }

        Page.Register(this);
    }

    protected override async Task OnInitializedAsync()
    {
        // Yielding first lets the page's SerginTreeSelect, rendered after the groups, name its node before it is read.
        await Task.Yield();
        await LoadRootsAsync();

        if (Page.RequestedNodeFor(this) is { } nodeId)
        {
            await ShowAsync(nodeId);
        }

        Page.GroupChanged();
    }

    private SerginTreeSelectedNode Describe(SerginTreeNodeState<TNode> state) => new(
        state.Id,
        LabelOf(state),
        [.. state.SelfAndAncestors().Skip(1).Reverse().Select(step => new SerginTreePathStep(step.Id, LabelOf(step)))],
        DynamicActions?.Invoke(state.Node) ?? []);

    private async Task LoadRootsAsync()
    {
        ErrorOr<IReadOnlyCollection<TNode>> loaded = await Roots(CancellationToken.None);

        if (loaded.IsError)
        {
            problem = ErrorPresenter.Present(loaded.FirstError);
            roots = [];
            selected = null;

            return;
        }

        problem = null;
        roots = [.. loaded.Value.Select(node => new SerginTreeNodeState<TNode>(node, NodeId(node), parent: null))];
        selected = null;

        await RestoreAsync(roots);

        if (filterTerm is not null)
        {
            Filter(filterTerm);
        }
    }

    // Re-expands what was open, and with Eager loads every level.
    private async Task RestoreAsync(IReadOnlyList<SerginTreeNodeState<TNode>> level)
    {
        foreach (SerginTreeNodeState<TNode> state in level)
        {
            bool reopen = expandedIds.Contains(state.Id);

            if ((reopen || Eager) && state.Children is null && Children is not null)
            {
                await LoadChildrenAsync(state, render: false);
            }

            state.Expanded = reopen && state.Children is { Count: > 0 };

            if (state.Children is { } children)
            {
                await RestoreAsync(children);
            }
        }
    }

    private async Task LoadChildrenAsync(SerginTreeNodeState<TNode> state, bool render = true)
    {
        if (Children is null)
        {
            state.Children = [];

            return;
        }

        state.Loading = true;

        if (render)
        {
            StateHasChanged();
        }

        ErrorOr<IReadOnlyCollection<TNode>> loaded = await Children(state.Node, CancellationToken.None);
        state.Loading = false;

        if (loaded.IsError)
        {
            ErrorPresenter.Notify(loaded.FirstError);
            state.Expanded = false;
            expandedIds.Remove(state.Id);

            return;
        }

        state.Children = [.. loaded.Value.Select(node => new SerginTreeNodeState<TNode>(node, NodeId(node), state))];
    }

    // Walks the path from a root, loading each level on the way, and stops where the tree no longer matches.
    private async Task<SerginTreeNodeState<TNode>?> LoadPathAsync(string nodeId)
    {
        if (PathTo is null || roots is null)
        {
            return null;
        }

        ErrorOr<IReadOnlyList<TNode>> path = await PathTo(nodeId, CancellationToken.None);

        if (path.IsError || path.Value.Count == 0)
        {
            return null;
        }

        IReadOnlyList<SerginTreeNodeState<TNode>> level = roots;
        SerginTreeNodeState<TNode>? current = null;

        foreach (TNode step in path.Value)
        {
            string stepId = NodeId(step);
            current = level.FirstOrDefault(state => string.Equals(state.Id, stepId, StringComparison.Ordinal));

            if (current is null)
            {
                return null;
            }

            if (string.Equals(stepId, nodeId, StringComparison.Ordinal))
            {
                return current;
            }

            if (current.Children is null)
            {
                await LoadChildrenAsync(current, render: false);
            }

            level = current.Children ?? [];
        }

        return null;
    }

    private SerginTreeNodeState<TNode>? Find(string nodeId) =>
        All().FirstOrDefault(state => string.Equals(state.Id, nodeId, StringComparison.Ordinal));

    private IEnumerable<SerginTreeNodeState<TNode>> All()
    {
        Stack<SerginTreeNodeState<TNode>> pending = new(Enumerable.Reverse(roots ?? []));

        while (pending.TryPop(out SerginTreeNodeState<TNode>? state))
        {
            yield return state;

            foreach (SerginTreeNodeState<TNode> child in Enumerable.Reverse(state.Children ?? []))
            {
                pending.Push(child);
            }
        }
    }

    // The rows as the panel shows them, top to bottom.
    private List<SerginTreeNodeState<TNode>> VisibleRows()
    {
        List<SerginTreeNodeState<TNode>> rows = [];

        void Visit(IReadOnlyList<SerginTreeNodeState<TNode>> level)
        {
            foreach (SerginTreeNodeState<TNode> state in level.Where(IsVisible))
            {
                rows.Add(state);

                if (IsExpanded(state) && state.Children is { } children)
                {
                    Visit(children);
                }
            }
        }

        Visit(roots ?? []);

        return rows;
    }

    private SerginTreeNodeState<TNode>? Neighbour(SerginTreeNodeState<TNode> state, int step)
    {
        List<SerginTreeNodeState<TNode>> rows = VisibleRows();
        int index = rows.IndexOf(state) + step;

        return index >= 0 && index < rows.Count ? rows[index] : null;
    }

    private async Task FocusAsync(SerginTreeNodeState<TNode>? state)
    {
        if (state is null || !RowRefs.TryGetValue(state.Id, out ElementReference row))
        {
            return;
        }

        cursorId = state.Id;
        await row.FocusAsync();
    }

    private Task ActivateAsync() => Page.ActivateAsync(this);
}
