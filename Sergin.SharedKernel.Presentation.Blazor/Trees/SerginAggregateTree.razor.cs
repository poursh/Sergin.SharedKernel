using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Domain.Securities;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Navigation;
using Sergin.SharedKernel.Presentation.Blazor.Pages;

namespace Sergin.SharedKernel.Presentation.Blazor.Trees;

/// <summary>
/// A <see cref="SerginTree"/> for one self-referencing aggregate — nodes of one type, each naming its parent, like
/// scopes, inside a module's tree layout: <c>&lt;SerginAggregateTree TNode="ScopeTreeItem" Title="Scopes" Href="/ua/scopes" Load="…" NodeHref="…" …&gt;@Body&lt;/SerginAggregateTree&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Load"/> reads the whole tree as a flat list once (and again on refresh or after a delete);
/// <see cref="SerginTreeIndex{TNode}"/> answers children, child counts and paths from it, so the filter sees every
/// node and a node selected from the URL opens with its ancestors expanded. A node whose parent the list does not hold
/// is a root.
/// </para>
/// <para>
/// The node menu is built here, in the wireframe's order: Open, the add-child action, the page's own
/// <see cref="NodeActions"/>, Information (with <see cref="LoadAudit"/>), Delete (with <see cref="Delete"/>, confirmed
/// through <see cref="SerginDialogExtensions.ConfirmDeleteAsync"/>, refused while the node has children unless
/// <see cref="DeleteWithChildren"/>). The add-child action is also the selected node's dynamic action.
/// </para>
/// </remarks>
public sealed partial class SerginAggregateTree<TNode>
    where TNode : notnull
{
    private SerginTreeGroup<TNode> group = default!;
    private SerginTreeIndex<TNode>? index;

    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    [Parameter]
    public IReadOnlyList<SerginBreadcrumb> Trail { get; set; } = [];

    [Parameter]
    public RenderFragment? HeaderActions { get; set; }

    [Parameter]
    public IReadOnlyList<SerginPageAction> StaticActions { get; set; } = [];

    /// <summary>Reads the whole tree as a flat list.</summary>
    [Parameter]
    [EditorRequired]
    public Func<Task<ErrorOr<IReadOnlyCollection<TNode>>>> Load { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public Func<TNode, string> NodeId { get; set; } = default!;

    /// <summary>The parent's id; null for a root.</summary>
    [Parameter]
    [EditorRequired]
    public Func<TNode, string?> ParentId { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public Func<TNode, string> NodeLabel { get; set; } = default!;

    [Parameter]
    public Func<TNode, string?>? NodeIcon { get; set; }

    /// <summary>Adds a root: the static row's action.</summary>
    [Parameter]
    public SerginPageAction? AddRoot { get; set; }

    /// <summary>Adds a child under a node: its dynamic action and a menu item; null for none.</summary>
    [Parameter]
    public Func<TNode, SerginPageAction?>? AddChild { get; set; }

    /// <summary>More menu items for a node, after the add-child action.</summary>
    [Parameter]
    public Func<TNode, IReadOnlyList<SerginPageAction>>? NodeActions { get; set; }

    /// <summary>Loads a node's audit for the Information item; null leaves the item out.</summary>
    [Parameter]
    public Func<TNode, Task<ErrorOr<AuditInfo>>>? LoadAudit { get; set; }

    /// <summary>Deletes a node for the Delete item; null leaves the item out.</summary>
    [Parameter]
    public Func<TNode, Task<ErrorOr<Deleted>>>? Delete { get; set; }

    [Parameter]
    public PermissionDefinition? DeletePermission { get; set; }

    /// <summary>Checks <see cref="DeletePermission"/> on any scope — for an aggregate that is not <c>Scoped()</c>.</summary>
    [Parameter]
    public bool DeleteAnyScope { get; set; }

    /// <summary>Offers Delete on a node that has children.</summary>
    [Parameter]
    public bool DeleteWithChildren { get; set; }

    /// <summary>The confirmation's title, e.g. <c>Delete scope</c>.</summary>
    [Parameter]
    public string DeleteTitle { get; set; } = "Delete";

    /// <summary>The tree's own route, where nothing is selected.</summary>
    [Parameter]
    [EditorRequired]
    public string Href { get; set; } = string.Empty;

    /// <summary>The route of a node's page: <c>node => $"/ua/scopes/{node.Id}"</c>.</summary>
    [Parameter]
    [EditorRequired]
    public Func<TNode, string> NodeHref { get; set; } = default!;

    /// <summary>The content side: the layout's <c>@Body</c>.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>More <see cref="SerginTreeGroup{TNode}"/>s, after this aggregate's.</summary>
    [Parameter]
    public RenderFragment? AdditionalGroups { get; set; }

    [Parameter]
    public string GroupKey { get; set; } = "node";

    /// <summary>The group's title; the page title when null.</summary>
    [Parameter]
    public string? GroupTitle { get; set; }

    [Parameter]
    public string? GroupIcon { get; set; }

    [Parameter]
    public string? LayoutKey { get; set; }

    [Parameter]
    public int PanelWidth { get; set; } = 260;

    [Parameter]
    public string FilterPlaceholder { get; set; } = "Filter tree…";

    [Parameter]
    public string EmptyText { get; set; } = "Nothing here yet.";

    [Inject]
    private IDialogService Dialogs { get; set; } = default!;

    [Inject]
    private IUiErrorPresenter ErrorPresenter { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    /// <summary>Reads the tree again.</summary>
    public Task ReloadAsync() => group.ReloadAsync();

    private async Task<ErrorOr<IReadOnlyCollection<TNode>>> LoadRootsAsync(CancellationToken cancellationToken)
    {
        ErrorOr<IReadOnlyCollection<TNode>> loaded = await Load();

        if (loaded.IsError)
        {
            index = null;

            return loaded.Errors;
        }

        index = new SerginTreeIndex<TNode>(loaded.Value, NodeId, ParentId, NodeLabel);

        IReadOnlyCollection<TNode> roots = [.. index.Roots];

        return roots.ToErrorOr();
    }

    private Task<ErrorOr<IReadOnlyCollection<TNode>>> LoadChildrenAsync(TNode node, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<TNode> children = [.. index?.ChildrenOf(NodeId(node)) ?? []];

        return Task.FromResult(children.ToErrorOr());
    }

    private Task<ErrorOr<IReadOnlyList<TNode>>> PathToAsync(string nodeId, CancellationToken cancellationToken)
    {
        IReadOnlyList<TNode> path = index?.PathTo(nodeId) ?? [];

        return Task.FromResult(path.ToErrorOr());
    }

    private int? CountOf(TNode node) => index?.ChildCount(NodeId(node));

    private IReadOnlyList<SerginPageAction> DynamicActionsOf(TNode node) =>
        AddChild?.Invoke(node) is { } add ? [add] : [];

    private List<SerginPageAction> MenuOf(TNode node)
    {
        List<SerginPageAction> menu =
        [
            new("Open", Icons.Material.Outlined.OpenInBrowser, SerginActionKind.Secondary,
                Href: NodeHref(node)),
        ];

        if (AddChild?.Invoke(node) is { } add)
        {
            menu.Add(add);
        }

        menu.AddRange(NodeActions?.Invoke(node) ?? []);

        if (LoadAudit is { } loadAudit)
        {
            menu.Add(new("Information", Icons.Material.Outlined.Info, SerginActionKind.Secondary,
                OnClick: EventCallback.Factory.Create(this, () => Dialogs.ShowInfoAsync(Localizer["Information"].Value, () => loadAudit(node)))));
        }

        if (Delete is not null)
        {
            menu.Add(new("Delete", Icons.Material.Outlined.Delete, SerginActionKind.Danger,
                OnClick: EventCallback.Factory.Create(this, () => DeleteAsync(node)),
                Permission: DeletePermission, AnyScope: DeleteAnyScope,
                Disabled: !DeleteWithChildren && CountOf(node) > 0));
        }

        return menu;
    }

    private async Task DeleteAsync(TNode node)
    {
        if (Delete is null || !await Dialogs.ConfirmDeleteAsync(
                Localizer[DeleteTitle].Value, $"Delete {NodeLabel(node)}? It disappears from every list and page."))
        {
            return;
        }

        ErrorOr<Deleted> result = await Delete(node);

        if (result.IsError)
        {
            ErrorPresenter.Notify(result.Errors);

            return;
        }

        // A deleted node that the page shows sends the tree back to its own route.
        await group.ReloadAsync();
    }
}
