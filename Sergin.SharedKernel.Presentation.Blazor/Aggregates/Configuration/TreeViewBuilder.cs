namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>The non-generic half, so the registry can read what any closed builder collected.</summary>
public abstract class TreeViewBuilder
{
    private protected TreeViewBuilder(Type item) => Item = item;

    /// <summary>The tree query's item type the configuration named.</summary>
    internal Type Item { get; }

    /// <summary>The item's title, from <c>Title(...)</c>; null for the item's first <see cref="string"/> property.</summary>
    internal Func<object, string>? TitleOf { get; private protected set; }

    /// <summary>An item's icon, from <c>Icon(...)</c>; null for none.</summary>
    internal Func<object, string?>? IconOf { get; private protected set; }

    internal TextDefinition? FilterPlaceholder { get; private protected set; }

    internal TextDefinition? EmptyText { get; private protected set; }

    internal TextDefinition? PromptTitle { get; private protected set; }

    internal TextDefinition? PromptHint { get; private protected set; }

    internal string? PromptIcon { get; private protected set; }

    internal TextDefinition? ChildrenTitle { get; private protected set; }

    internal TextDefinition? ChildrenEmpty { get; private protected set; }
}

/// <summary>
/// How a root's tree (its <c>ITreeQuery</c>, whose items carry a <see cref="Guid"/> <c>Id</c> and a nullable
/// <see cref="Guid"/> <c>ParentId</c>) is shown where a view lists it, as the Move dialog's parent picker does.
/// </summary>
public sealed class TreeViewBuilder<TItem> : TreeViewBuilder
{
    internal TreeViewBuilder()
        : base(typeof(TItem))
    {
    }

    /// <summary>The item's title, as a picker shows it; the item's first <see cref="string"/> property by default.</summary>
    public TreeViewBuilder<TItem> Title(Func<TItem, string> title)
    {
        ArgumentNullException.ThrowIfNull(title);

        TitleOf = item => title((TItem)item);
        return this;
    }

    /// <summary>The icon of an item (a MudBlazor icon string), or null for none; no icon by default.</summary>
    public TreeViewBuilder<TItem> Icon(Func<TItem, string?> icon)
    {
        ArgumentNullException.ThrowIfNull(icon);

        IconOf = item => icon((TItem)item);
        return this;
    }

    /// <summary>The placeholder of the tree's filter box.</summary>
    public TreeViewBuilder<TItem> Filter(TextDefinition placeholder)
    {
        ArgumentNullException.ThrowIfNull(placeholder);

        FilterPlaceholder = placeholder;
        return this;
    }

    /// <summary>What the tree shows when it has no items.</summary>
    public TreeViewBuilder<TItem> Empty(TextDefinition text)
    {
        ArgumentNullException.ThrowIfNull(text);

        EmptyText = text;
        return this;
    }

    /// <summary>What the tree's page shows while no item is selected: a title, an optional hint and an icon.</summary>
    public TreeViewBuilder<TItem> Prompt(TextDefinition title, TextDefinition? hint = null, string? icon = null)
    {
        ArgumentNullException.ThrowIfNull(title);

        PromptTitle = title;
        PromptHint = hint;
        PromptIcon = icon;
        return this;
    }

    /// <summary>The heading of an item's children list and what it shows when the item has none.</summary>
    public TreeViewBuilder<TItem> Children(TextDefinition title, TextDefinition empty)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(empty);

        ChildrenTitle = title;
        ChildrenEmpty = empty;
        return this;
    }
}
