using System.Collections;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;
using Sergin.SharedKernel.Presentation.Blazor.Pages;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views;

/// <summary>
/// The body of a tree layout for a configured root, generated from its model: <c>&lt;SerginGeneratedTree TRoot="Scope"&gt;@Body&lt;/SerginGeneratedTree&gt;</c>.
/// It renders a <see cref="SerginAggregateTree{TNode}"/> over the root's tree query, with the node menu's add-child action
/// (the self-parented create command), Delete (the root's shown delete command, sent at the version read just before it when
/// guarded) and Information (the audit query). Rendering a root with no tree, or no list or detail route, throws.
/// </summary>
public sealed partial class SerginGeneratedTree<TRoot>
    where TRoot : class, IAggregateRoot
{
    private AggregateViewModel model = default!;
    private GeneratedTreeParts parts = default!;

    /// <summary>The page at the current route, shown beside the tree.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>More groups of the tree, with several groups the tree is an accordion.</summary>
    [Parameter]
    public RenderFragment? AdditionalGroups { get; set; }

    [Inject]
    private AggregateViewRegistry Views { get; set; } = default!;

    [Inject]
    private ISerginDispatcher Dispatcher { get; set; } = default!;

    protected override void OnInitialized()
    {
        model = Views.For<TRoot>();
        parts = GeneratedTreeParts.For(model);
    }

    private string IdOf(object node) => parts.Tree.IdOf(node).ToString();

    private string? ParentIdOf(object node) => parts.Tree.ParentIdOf(node)?.ToString();

    private string HrefOf(object node) => parts.NodeHref(parts.Tree.IdOf(node));

    private SerginPageAction AddChildOf(object node)
    {
        CommandView add = parts.AddChild!;

        // A create command requiring several permissions is not supported here: the action carries one.
        return new SerginPageAction(
            add.Label,
            add.Icon ?? Icons.Material.Outlined.Add,
            SerginActionKind.Secondary,
            Href: model.ChildCreateHref(parts.Tree.IdOf(node)),
            Permission: add.Permissions.FirstOrDefault(),
            AnyScope: add.AnyScope);
    }

    private async Task<ErrorOr<IReadOnlyCollection<object>>> LoadAsync() =>
        NodesOf(await parts.Tree.Query.Invoker.SendAsync(Dispatcher, parts.Tree.Query.Build()));

    private static ErrorOr<IReadOnlyCollection<object>> NodesOf(ViewSendResult result)
    {
        if (result.IsError)
        {
            return ErrorsOf(result);
        }

        IReadOnlyCollection<object> nodes = [.. ((IEnumerable)result.Value!).Cast<object>()];
        return ErrorOrFactory.From(nodes);
    }

    private async Task<ErrorOr<Deleted>> DeleteAsync(object node)
    {
        CommandView delete = parts.Delete!;
        Guid id = parts.Tree.IdOf(node);
        RowVersion? version = null;

        if (delete.RequiresVersion && model.GetOne is not null)
        {
            ViewSendResult loaded = await model.LoadRecordAsync(Dispatcher, id);

            if (loaded.IsError)
            {
                return ErrorsOf(loaded);
            }

            version = loaded.Version;
        }

        ViewSendResult result = await GeneratedViewSupport.SendAsync(Dispatcher, delete, GeneratedTreeParts.DeleteRequest(delete, id), version);

        return result.IsError ? ErrorsOf(result) : Result.Deleted;
    }

    private async Task<ErrorOr<AuditInfo>> LoadAuditAsync(object node)
    {
        ViewSendResult result = await model.Audit!.Invoker.SendAsync(Dispatcher, model.Audit.Build(parts.Tree.IdOf(node)));

        return result.IsError ? ErrorsOf(result) : (AuditInfo)result.Value!;
    }

    private static List<Error> ErrorsOf(ViewSendResult result) => [.. result.Errors];
}
