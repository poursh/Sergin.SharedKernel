using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>The non-generic half, so the registry can read what any closed builder collected.</summary>
public abstract class AggregateViewBuilder : ViewDeclarationBuilder
{
    private protected AggregateViewBuilder(Type root) => Root = root;

    internal Type Root { get; }

    internal TextDefinition? ListTitle { get; private protected set; }

    internal AggregateViewRoutes DeclaredRoutes { get; private protected set; } = AggregateViewRoutes.None;

    /// <summary>The parent root a <c>ListUnder&lt;TParent&gt;</c> named, or null.</summary>
    internal Type? ListParent { get; private protected set; }

    /// <summary>What <c>BelongsTo&lt;TParent, TResponse&gt;(...)</c> declared, or null.</summary>
    internal BelongsToDeclaration? DeclaredBelongsTo { get; private protected set; }

    /// <summary>What <c>Tree&lt;TItem&gt;(...)</c> declared, or null.</summary>
    internal TreeViewBuilder? DeclaredTree { get; private protected set; }

    internal Dictionary<SerginViewSlot, Type> Slots { get; } = [];

    internal bool DetailOverviewIsCollapsed { get; private protected set; }

    /// <summary>The child entities the configuration declared, by child type.</summary>
    internal Dictionary<Type, ChildViewBuilder> Children { get; } = [];

    /// <summary>The links to other pages the configuration declared, in order.</summary>
    internal List<ViewLinkView> Links { get; } = [];
}

/// <summary>What a view configuration declares for <typeparamref name="TRoot"/>.</summary>
public sealed class AggregateViewBuilder<TRoot> : AggregateViewBuilder
    where TRoot : class, IAggregateRoot
{
    internal AggregateViewBuilder()
        : base(typeof(TRoot))
    {
    }

    /// <summary>The list page's title; detail and form titles are the root's <c>Named(...)</c> name.</summary>
    public AggregateViewBuilder<TRoot> Title(TextDefinition title)
    {
        ArgumentNullException.ThrowIfNull(title);
        ListTitle = title;
        return this;
    }

    /// <summary>
    /// Where the root's pages live; <paramref name="detail"/> and <paramref name="edit"/> build a record's route from its
    /// id. Each must match a routable component's <c>[Route]</c>. <paramref name="list"/> is the page of the root's
    /// <c>IListOf</c> query or, for a root read as a tree (<c>ITreeQuery</c>), the tree's page; a root with neither, such
    /// as one that belongs to a parent (<see cref="BelongsTo{TParent, TResponse}"/>), names none.
    /// </summary>
    public AggregateViewBuilder<TRoot> Routes(
        string? list = null, Func<Guid, string>? detail = null, string? create = null, Func<Guid, string>? edit = null)
    {
        if (list is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(list);
        }

        ArgumentNullException.ThrowIfNull(detail);
        DeclaredRoutes = new AggregateViewRoutes(list, detail, create, edit, DeclaredRoutes.ListUnderParent);
        return this;
    }

    /// <summary>
    /// The list route of a root whose list query is <c>IUnderParent&lt;TParent, …&gt;</c>, built from the parent's id. Its
    /// detail, create and edit routes stay optional; declare them with <see cref="Routes"/> only if the root has them.
    /// </summary>
    public AggregateViewBuilder<TRoot> ListUnder<TParent>(Func<Guid, string> list)
        where TParent : class, IAggregateRoot
    {
        ArgumentNullException.ThrowIfNull(list);
        ListParent = typeof(TParent);
        DeclaredRoutes = DeclaredRoutes with { ListUnderParent = list };
        return this;
    }

    /// <summary>
    /// The record belongs to a record of <typeparamref name="TParent"/> (a role to its owner scope, a scope to its parent
    /// scope), read from <typeparamref name="TResponse"/>, the GetOne query's response: <paramref name="parentId"/> is the
    /// parent's id (null for a record with no parent) and <paramref name="parentName"/> its name as stored. The detail
    /// trail is the parent's list step, the parent linking to its detail, then the record; a delete leaves for the
    /// parent's detail, else the parent's list, else the home page; and a create form given the parent's id names it in
    /// its trail and cancels to it. The parent needs a view configuration with a detail route; a root listed under a
    /// parent (<see cref="ListUnder{TParent}"/>) declares no <c>BelongsTo</c>.
    /// </summary>
    public AggregateViewBuilder<TRoot> BelongsTo<TParent, TResponse>(Func<TResponse, Guid?> parentId, Func<TResponse, string?> parentName)
        where TParent : class, IAggregateRoot
    {
        ArgumentNullException.ThrowIfNull(parentId);
        ArgumentNullException.ThrowIfNull(parentName);

        DeclaredBelongsTo = new BelongsToDeclaration(
            typeof(TParent), typeof(TResponse), record => parentId((TResponse)record), record => parentName((TResponse)record));
        return this;
    }

    /// <summary>How the command <typeparamref name="TCommand"/>, which must be typed to <typeparamref name="TRoot"/>, is offered.</summary>
    public CommandViewBuilder<TCommand> Command<TCommand>()
        where TCommand : IBaseCommand => DeclareCommand<TCommand>();

    /// <summary>
    /// How the root's tree is shown where a view lists it (the Move dialog's parent picker): <typeparamref name="TItem"/>
    /// must be the item of the root's <c>ITreeQuery</c>, whose constructor takes no arguments and whose items carry a
    /// <see cref="Guid"/> <c>Id</c> and a nullable <see cref="Guid"/> <c>ParentId</c>.
    /// </summary>
    public AggregateViewBuilder<TRoot> Tree<TItem>(Action<TreeViewBuilder<TItem>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        TreeViewBuilder<TItem> tree = DeclaredTree as TreeViewBuilder<TItem> ?? new TreeViewBuilder<TItem>();
        configure(tree);
        DeclaredTree = tree;
        return this;
    }

    /// <summary>Overrides for the detail fields; <typeparamref name="TResponse"/> must be the GetOne query's response.</summary>
    public AggregateViewBuilder<TRoot> Detail<TResponse>(Action<FieldSetBuilder<TResponse>> configure)
    {
        DeclareDetail(configure);
        return this;
    }

    /// <summary>Overrides for the list columns and filters; <typeparamref name="TItem"/> must be the list query's item.</summary>
    public AggregateViewBuilder<TRoot> List<TItem>(Action<ListViewBuilder<TItem>> configure)
    {
        DeclareList(configure);
        return this;
    }

    /// <summary>Overrides for the form of <typeparamref name="TCommand"/>, the root's create or update command.</summary>
    public AggregateViewBuilder<TRoot> Form<TCommand>(Action<FormViewBuilder<TCommand>> configure)
        where TCommand : IBaseCommand
    {
        DeclareForm(configure);
        return this;
    }

    /// <summary>
    /// How the child entity <typeparamref name="TChild"/> of <typeparamref name="TRoot"/> is shown and edited: its name,
    /// routes, commands, fields and forms. A child the root's requests name is shown whether or not it is declared here,
    /// but needs <c>Named(...)</c>, so declare each one.
    /// </summary>
    public AggregateViewBuilder<TRoot> Child<TChild>(Action<ChildViewBuilder<TRoot, TChild>> configure)
        where TChild : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(configure);

        if (!Children.TryGetValue(typeof(TChild), out ChildViewBuilder? existing))
        {
            existing = new ChildViewBuilder<TRoot, TChild>();
            Children.Add(typeof(TChild), existing);
        }

        configure((ChildViewBuilder<TRoot, TChild>)existing);
        return this;
    }

    /// <summary>
    /// A link to another page: a Secondary action on the detail bar and an icon on each list row. <paramref name="href"/>
    /// builds the page's route from the record's id and must match a routable component's <c>[Route]</c>. It carries no
    /// permission: it is shown to whoever sees the view, and the target page refuses on its own.
    /// </summary>
    public AggregateViewBuilder<TRoot> Link(TextDefinition label, string icon, Func<Guid, string> href)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(icon);
        ArgumentNullException.ThrowIfNull(href);
        Links.Add(new ViewLinkView(label, icon, href));
        return this;
    }

    /// <summary>Starts the detail view's overview collapsed.</summary>
    public AggregateViewBuilder<TRoot> DetailOverviewCollapsed()
    {
        DetailOverviewIsCollapsed = true;
        return this;
    }

    /// <summary>Puts <typeparamref name="TComponent"/> in <paramref name="slot"/> of the generated views.</summary>
    public AggregateViewBuilder<TRoot> Slot<TComponent>(SerginViewSlot slot)
        where TComponent : IComponent
    {
        Slots[slot] = typeof(TComponent);
        return this;
    }
}

/// <summary>A <c>BelongsTo&lt;TParent, TResponse&gt;(...)</c> declaration: the parent root, the response it reads, and its two readers.</summary>
internal sealed record BelongsToDeclaration(Type Parent, Type Response, Func<object, Guid?> ParentId, Func<object, string?> ParentName);
