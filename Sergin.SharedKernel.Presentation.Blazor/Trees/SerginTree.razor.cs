using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Sergin.SharedKernel.Presentation.Blazor.Navigation;
using Sergin.SharedKernel.Presentation.Blazor.Pages;
using Sergin.SharedKernel.Presentation.Blazor.Views;

namespace Sergin.SharedKernel.Presentation.Blazor.Trees;

/// <summary>
/// A tree on the left and the page at the current route on the right, rendered by a module's tree layout:
/// <c>&lt;SerginTree Title="Scopes" Href="/ua/scopes"&gt;&lt;Groups&gt;…&lt;/Groups&gt;&lt;Content&gt;@Body&lt;/Content&gt;&lt;/SerginTree&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every page that names the layout renders inside it, and Blazor keeps the layout — and this tree, with what is
/// expanded, scrolled and filtered — while the user moves between those pages. The route is the selection: a click
/// navigates to the node's href (<see cref="SerginTreeGroup{TNode}.NodeHref"/>), and the page there names its node
/// with a <see cref="SerginTreeSelect"/>. A page that names none, like the tree's own route (<see cref="Href"/>), leaves
/// nothing selected.
/// </para>
/// <para>
/// The tree cascades a <see cref="SerginViewHostKind.Pane"/> <see cref="SerginViewHost"/> to its content, so a view
/// keeps the tree's title, the trail goes on with the selected node's path, and the view's header buttons follow the
/// tree's in the page header. Leaving reads the tree again and navigates; a change that keeps the view open reads the
/// tree again.
/// </para>
/// <para>
/// The groups (<see cref="SerginTreeGroup{TNode}"/>, one per data source) go in <see cref="Groups"/>; one group reads as
/// a plain tree. With several, exactly one is active and expanded, like an accordion; switching leaves to
/// <see cref="Href"/>, the filter searches the active group only, and the last active group, the panel width and whether
/// it is hidden are kept per browser (<see cref="IUiTreeLayoutStore"/>) under <see cref="LayoutKey"/>.
/// </para>
/// </remarks>
public sealed partial class SerginTree : IDisposable
{
    private const int MinWidth = 200;
    private const int MaxWidth = 480;

    private readonly List<ISerginTreeGroup> groups = [];
    private readonly SerginHeaderSlot headerSlot = new();
    private SerginViewHost paneHost = default!;
    private object? owner;
    private string? requestedGroup;
    private string? requestedNode;
    private string? tail;
    private bool leaving;
    private string? activeKey;
    private string? filter;
    private int width;
    private bool hidden;
    private bool dragging;
    private double dragStartX;
    private int dragStartWidth;
    private bool paneHasActions;

    /// <summary>The page title and the panel's accessible name (localised).</summary>
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>The tree's own route, where nothing is selected: the close button, Esc and a deleted node go there.</summary>
    [Parameter]
    [EditorRequired]
    public string Href { get; set; } = string.Empty;

    /// <summary>
    /// The tree's steps below home; the last is the tree, a link on every page but <see cref="Href"/>. The selected
    /// node's path follows it.
    /// </summary>
    [Parameter]
    public IReadOnlyList<SerginBreadcrumb> Trail { get; set; } = [];

    /// <summary>The page header's icon buttons; the pane view's follow them.</summary>
    [Parameter]
    public RenderFragment? HeaderActions { get; set; }

    /// <summary>Tree-level actions, first in the static row; a one-group tree adds the group's add action after them.</summary>
    [Parameter]
    public IReadOnlyList<SerginPageAction> StaticActions { get; set; } = [];

    /// <summary>The groups.</summary>
    [Parameter]
    public RenderFragment? Groups { get; set; }

    /// <summary>The content side: the layout's <c>@Body</c>.</summary>
    [Parameter]
    public RenderFragment? Content { get; set; }

    /// <summary>The key the panel layout is kept under in the browser; null keeps nothing.</summary>
    [Parameter]
    public string? LayoutKey { get; set; }

    [Parameter]
    public int PanelWidth { get; set; } = 260;

    [Parameter]
    public bool Resizable { get; set; } = true;

    [Parameter]
    public bool Collapsible { get; set; } = true;

    [Parameter]
    public string FilterPlaceholder { get; set; } = "Filter tree…";

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IUiTreeLayoutStore LayoutStore { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    internal bool ShowGroupHeaders => groups.Count > 1;

    private bool AtRoot => string.Equals(
        new Uri(Navigation.Uri).AbsolutePath.TrimEnd('/'),
        Navigation.ToAbsoluteUri(Href).AbsolutePath.TrimEnd('/'),
        StringComparison.OrdinalIgnoreCase);

    private ISerginTreeGroup? ActiveGroup => groups.Find(group => string.Equals(group.Key, activeKey, StringComparison.Ordinal));

    private SerginTreeSelectedNode? SelectedNode => ActiveGroup?.Selected;

    private IReadOnlyList<SerginPageAction> StaticRowActions =>
        groups.Count == 1 && groups[0].AddAction is { } add ? [.. StaticActions, add] : StaticActions;

    private bool HasHeaderActions => HeaderActions is not null || paneHasActions;

    // The tree's own step stays a link wherever a page shows, unless the page named nothing to put after it.
    private bool LinkLast => !AtRoot && SelectedNode is null && tail is null;

    // The tree's trail, then the selected node's path: the group with several, the ancestors, the node — a link when
    // the page shows something under it — and the page's tail.
    private IReadOnlyList<SerginBreadcrumb> PageTrail
    {
        get
        {
            if (SelectedNode is null && tail is null)
            {
                return Trail;
            }

            List<SerginBreadcrumb> trail = [.. Trail];

            if (ShowGroupHeaders && ActiveGroup is { } group)
            {
                trail.Add(new SerginBreadcrumb(group.Title, Href));
            }

            if (SelectedNode is { } node)
            {
                trail.AddRange(node.Ancestors.Select(step => new SerginBreadcrumb(step.Label, ActiveGroup?.HrefFor(step.Id))));
                trail.Add(new SerginBreadcrumb(node.Label, tail is null ? null : ActiveGroup?.HrefFor(node.Id)));
            }

            if (tail is not null)
            {
                trail.Add(new SerginBreadcrumb(tail));
            }

            return trail;
        }
    }

    private string WidthStyle => string.Create(CultureInfo.InvariantCulture, $"--sergin-tree-width: {width}px");

    public void Dispose() => headerSlot.Changed -= OnHeaderSlotChanged;

    /// <summary>Back to nothing selected: navigates to <see cref="Href"/>.</summary>
    public Task ClearSelectionAsync()
    {
        if (!AtRoot)
        {
            Navigation.NavigateTo(Href);
        }

        return Task.CompletedTask;
    }

    /// <summary>Loads every group again.</summary>
    public async Task RefreshAsync()
    {
        foreach (ISerginTreeGroup group in groups.ToList())
        {
            await group.ReloadAsync();
        }
    }

    internal void Register(ISerginTreeGroup group)
    {
        if (groups.Exists(existing => string.Equals(existing.Key, group.Key, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Two SerginTreeGroups in one SerginTree use the key '{group.Key}'.");
        }

        groups.Add(group);
        activeKey ??= group.Key;
        StateHasChanged();
    }

    internal void Unregister(ISerginTreeGroup group) => groups.Remove(group);

    internal bool IsActive(ISerginTreeGroup group) => string.Equals(group.Key, activeKey, StringComparison.Ordinal);

    internal void Navigate(string href) => Navigation.NavigateTo(href);

    /// <summary>
    /// A page's <see cref="SerginTreeSelect"/> names its node; a null <paramref name="group"/> means the active group.
    /// The marker becomes the owner of the selection.
    /// </summary>
    internal void Select(object marker, string? group, string nodeId, string? label)
    {
        owner = marker;
        tail = label;

        if (group is not null)
        {
            activeKey = group;
        }

        if (string.Equals(requestedNode, nodeId, StringComparison.Ordinal) && string.Equals(requestedGroup, group, StringComparison.Ordinal))
        {
            StateHasChanged();

            return;
        }

        requestedGroup = group;
        requestedNode = nodeId;
        _ = InvokeAsync(ApplyAsync);
    }

    /// <summary>
    /// A marker names no node, or went away: the selection clears, unless another marker owns it by now. A marker that
    /// names no node can still give the page header its <paramref name="label"/>.
    /// </summary>
    internal void Release(object marker, string? label = null)
    {
        if (owner is not null && !ReferenceEquals(owner, marker))
        {
            return;
        }

        owner = label is null ? null : marker;
        tail = label;

        if (requestedNode is null)
        {
            StateHasChanged();

            return;
        }

        requestedGroup = null;
        requestedNode = null;
        _ = InvokeAsync(ApplyAsync);
    }

    /// <summary>The node a page asked <paramref name="group"/> to show; a group reads it once its roots are in.</summary>
    internal string? RequestedNodeFor(ISerginTreeGroup group)
    {
        bool mine = requestedGroup is null ? IsActive(group) : string.Equals(requestedGroup, group.Key, StringComparison.Ordinal);

        return mine ? requestedNode : null;
    }

    /// <summary>The node the page shows no longer exists (deleted from the menu, say): back to <see cref="Href"/>.</summary>
    internal void SelectionGone(ISerginTreeGroup group)
    {
        if (!leaving && RequestedNodeFor(group) is not null)
        {
            Navigation.NavigateTo(Href);
        }
    }

    /// <summary>A group's selection or loading changed what the tree shows around it.</summary>
    internal void GroupChanged() => StateHasChanged();

    internal async Task ActivateAsync(ISerginTreeGroup group)
    {
        if (IsActive(group))
        {
            return;
        }

        ActiveGroup?.Filter(null);
        activeKey = group.Key;
        filter = null;

        await ClearSelectionAsync();
        await SaveLayoutAsync();
    }

    protected override void OnInitialized()
    {
        width = Math.Clamp(PanelWidth, MinWidth, MaxWidth);
        paneHost = new SerginViewHost(SerginViewHostKind.Pane, LeaveAsync, headerSlot, ReloadActiveAsync);
        headerSlot.Changed += OnHeaderSlotChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || LayoutKey is null)
        {
            return;
        }

        SerginTreeLayout layout = await LayoutStore.LoadAsync(LayoutKey);

        width = Math.Clamp(layout.Width ?? width, MinWidth, MaxWidth);
        hidden = Collapsible && layout.Hidden;

        // The page's node wins over the remembered group.
        if (requestedNode is null && layout.Group is { } key && groups.Exists(group => string.Equals(group.Key, key, StringComparison.Ordinal)))
        {
            activeKey = key;
        }

        StateHasChanged();
    }

    // Tells each loaded group what to show now; a group still loading reads it itself.
    private async Task ApplyAsync()
    {
        foreach (ISerginTreeGroup group in groups.Where(group => group.Loaded).ToList())
        {
            await group.ShowAsync(RequestedNodeFor(group));
            group.Refresh();
        }

        StateHasChanged();
    }

    // The view is done (saved, deleted, cancelled): the tree is read again, then the page goes where the view said.
    private async Task LeaveAsync(string href)
    {
        leaving = true;

        try
        {
            if (ActiveGroup is { } group)
            {
                await group.ReloadAsync();
            }
        }
        finally
        {
            leaving = false;
        }

        Navigation.NavigateTo(href);
    }

    // The view renamed or moved its record: the tree reads it again, keeping the selection.
    private Task ReloadActiveAsync() => ActiveGroup?.ReloadAsync() ?? Task.CompletedTask;

    // The outlet renders the view's buttons itself; the tree renders again only when the page header gains or loses
    // its buttons, since rendering the tree renders the view again, which writes the slot again.
    private void OnHeaderSlotChanged(object? sender, EventArgs args)
    {
        if (paneHasActions == headerSlot.Content is not null)
        {
            return;
        }

        paneHasActions = headerSlot.Content is not null;
        _ = InvokeAsync(StateHasChanged);
    }

    private void CollapseAll()
    {
        foreach (ISerginTreeGroup group in groups)
        {
            group.CollapseAll();
        }
    }

    private Task FilterChangedAsync(string? value)
    {
        filter = value;
        ActiveGroup?.Filter(value);

        return Task.CompletedTask;
    }

    private async Task ToggleHiddenAsync()
    {
        hidden = !hidden;
        await SaveLayoutAsync();
    }

    private void StartResize(PointerEventArgs args)
    {
        dragging = true;
        dragStartX = args.ClientX;
        dragStartWidth = width;
    }

    private void Resize(PointerEventArgs args) =>
        width = Math.Clamp(dragStartWidth + (int)Math.Round(args.ClientX - dragStartX), MinWidth, MaxWidth);

    private async Task EndResizeAsync(PointerEventArgs args)
    {
        if (!dragging)
        {
            return;
        }

        dragging = false;
        await SaveLayoutAsync();
    }

    private async Task SaveLayoutAsync()
    {
        if (LayoutKey is not null)
        {
            await LayoutStore.SaveAsync(LayoutKey, new SerginTreeLayout(width, hidden, activeKey));
        }
    }
}
