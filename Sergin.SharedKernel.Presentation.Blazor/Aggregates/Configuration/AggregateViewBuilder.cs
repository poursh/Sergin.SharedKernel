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
    /// id. Each must match a routable component's <c>[Route]</c>.
    /// </summary>
    public AggregateViewBuilder<TRoot> Routes(
        string list, Func<Guid, string> detail, string? create = null, Func<Guid, string>? edit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(list);
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

    /// <summary>How the command <typeparamref name="TCommand"/>, which must be typed to <typeparamref name="TRoot"/>, is offered.</summary>
    public CommandViewBuilder<TCommand> Command<TCommand>()
        where TCommand : IBaseCommand => DeclareCommand<TCommand>();

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
