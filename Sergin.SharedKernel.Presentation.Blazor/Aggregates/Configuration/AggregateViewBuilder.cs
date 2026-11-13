using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>The non-generic half, so the registry can read what any closed builder collected.</summary>
public abstract class AggregateViewBuilder
{
    private protected AggregateViewBuilder(Type root) => Root = root;

    internal Type Root { get; }

    internal TextDefinition? ListTitle { get; private protected set; }

    internal AggregateViewRoutes DeclaredRoutes { get; private protected set; } = AggregateViewRoutes.None;

    /// <summary>The commands the configuration named, by request type.</summary>
    internal Dictionary<Type, CommandViewBuilder> Commands { get; } = [];

    internal FieldSetBuilder? DetailFields { get; private protected set; }

    internal FieldSetBuilder? ListFields { get; private protected set; }

    /// <summary>The forms the configuration named, by command type.</summary>
    internal Dictionary<Type, FormViewBuilder> Forms { get; } = [];

    internal Dictionary<SerginViewSlot, Type> Slots { get; } = [];

    internal bool DetailOverviewIsCollapsed { get; private protected set; }
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
        DeclaredRoutes = new AggregateViewRoutes(list, detail, create, edit);
        return this;
    }

    /// <summary>How the command <typeparamref name="TCommand"/>, which must be typed to <typeparamref name="TRoot"/>, is offered.</summary>
    public CommandViewBuilder<TCommand> Command<TCommand>()
        where TCommand : IBaseCommand
    {
        if (Commands.TryGetValue(typeof(TCommand), out CommandViewBuilder? existing))
        {
            return (CommandViewBuilder<TCommand>)existing;
        }

        CommandViewBuilder<TCommand> command = new();
        Commands.Add(typeof(TCommand), command);
        return command;
    }

    /// <summary>Overrides for the detail fields; <typeparamref name="TResponse"/> must be the GetOne query's response.</summary>
    public AggregateViewBuilder<TRoot> Detail<TResponse>(Action<FieldSetBuilder<TResponse>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        FieldSetBuilder<TResponse> fields = DetailFields as FieldSetBuilder<TResponse> ?? new FieldSetBuilder<TResponse>();
        configure(fields);
        DetailFields = fields;
        return this;
    }

    /// <summary>Overrides for the list columns and filters; <typeparamref name="TItem"/> must be the list query's item.</summary>
    public AggregateViewBuilder<TRoot> List<TItem>(Action<ListViewBuilder<TItem>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        ListViewBuilder<TItem> fields = ListFields as ListViewBuilder<TItem> ?? new ListViewBuilder<TItem>();
        configure(fields);
        ListFields = fields;
        return this;
    }

    /// <summary>Overrides for the form of <typeparamref name="TCommand"/>, the root's create or update command.</summary>
    public AggregateViewBuilder<TRoot> Form<TCommand>(Action<FormViewBuilder<TCommand>> configure)
        where TCommand : IBaseCommand
    {
        ArgumentNullException.ThrowIfNull(configure);

        if (!Forms.TryGetValue(typeof(TCommand), out FormViewBuilder? form))
        {
            form = new FormViewBuilder<TCommand>();
            Forms.Add(typeof(TCommand), form);
        }

        configure((FormViewBuilder<TCommand>)form);
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
