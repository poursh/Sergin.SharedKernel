using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Configuration;

/// <summary>The non-generic half, so the registry can read what any closed builder collected.</summary>
public abstract class ChildViewBuilder : ViewDeclarationBuilder
{
    private protected ChildViewBuilder(Type root, Type child)
    {
        Root = root;
        Child = child;
    }

    internal Type Root { get; }

    internal Type Child { get; }

    internal TextDefinition? Singular { get; private protected set; }

    internal TextDefinition? SectionTitle { get; private protected set; }

    internal ChildViewRoutes DeclaredRoutes { get; private protected set; } = ChildViewRoutes.None;

    /// <summary>The component that draws the child, from <c>Section&lt;TComponent&gt;()</c>; null for the generated section.</summary>
    internal Type? SectionComponent { get; private protected set; }
}

/// <summary>What a view configuration declares for <typeparamref name="TChild"/>, a child entity of <typeparamref name="TRoot"/>.</summary>
public sealed class ChildViewBuilder<TRoot, TChild> : ChildViewBuilder
    where TRoot : class, IAggregateRoot
    where TChild : class, IEntity
{
    internal ChildViewBuilder()
        : base(typeof(TRoot), typeof(TChild))
    {
    }

    /// <summary>The child's name: the detail and form titles and the list's singular. Required.</summary>
    public ChildViewBuilder<TRoot, TChild> Named(TextDefinition singular)
    {
        ArgumentNullException.ThrowIfNull(singular);
        Singular = singular;
        return this;
    }

    /// <summary>The child list section's title; the <see cref="Named"/> name by default.</summary>
    public ChildViewBuilder<TRoot, TChild> Title(TextDefinition title)
    {
        ArgumentNullException.ThrowIfNull(title);
        SectionTitle = title;
        return this;
    }

    /// <summary>
    /// Draws the child with <typeparamref name="TComponent"/> in place of the generated child section, where that section
    /// would render on the root's detail view. The component receives the root's <see cref="AggregateViewContext"/> as its
    /// <c>[Parameter] AggregateViewContext Context</c> (the record, its version, the reload and the generation). Such a child
    /// needs only <see cref="Named"/>: its list, GetOne, add and remove requests are neither built nor checked (an unpaged
    /// <c>IChildListOf</c> is allowed), its commands are not offered by the generated views, and it declares no routes,
    /// list, detail, form or command.
    /// </summary>
    public ChildViewBuilder<TRoot, TChild> Section<TComponent>()
        where TComponent : IComponent
    {
        SectionComponent = typeof(TComponent);
        return this;
    }

    /// <summary>
    /// Where the child's pages live; the first <see cref="Guid"/> is always the root's id. Each must match a routable
    /// component's <c>[Route]</c>.
    /// </summary>
    public ChildViewBuilder<TRoot, TChild> Routes(
        Func<Guid, Guid, string>? detail = null, Func<Guid, string>? create = null, Func<Guid, Guid, string>? edit = null)
    {
        DeclaredRoutes = new ChildViewRoutes(detail, create, edit);
        return this;
    }

    /// <summary>How the command <typeparamref name="TCommand"/>, which must be typed to <typeparamref name="TChild"/>, is offered.</summary>
    public CommandViewBuilder<TCommand> Command<TCommand>()
        where TCommand : IBaseCommand => DeclareCommand<TCommand>();

    /// <summary>Overrides for the detail fields; <typeparamref name="TResponse"/> must be the child's GetOne response.</summary>
    public ChildViewBuilder<TRoot, TChild> Detail<TResponse>(Action<FieldSetBuilder<TResponse>> configure)
    {
        DeclareDetail(configure);
        return this;
    }

    /// <summary>Overrides for the list columns; <typeparamref name="TItem"/> must be the child list query's item.</summary>
    public ChildViewBuilder<TRoot, TChild> List<TItem>(Action<ListViewBuilder<TItem>> configure)
    {
        DeclareList(configure);
        return this;
    }

    /// <summary>Overrides for the form of <typeparamref name="TCommand"/>, the child's add or edit command.</summary>
    public ChildViewBuilder<TRoot, TChild> Form<TCommand>(Action<FormViewBuilder<TCommand>> configure)
        where TCommand : IBaseCommand
    {
        DeclareForm(configure);
        return this;
    }
}
