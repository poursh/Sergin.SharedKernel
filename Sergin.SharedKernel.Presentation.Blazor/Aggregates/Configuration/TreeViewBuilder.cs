namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>The non-generic half, so the registry can read what any closed builder collected.</summary>
public abstract class TreeViewBuilder
{
    private protected TreeViewBuilder(Type item) => Item = item;

    /// <summary>The tree query's item type the configuration named.</summary>
    internal Type Item { get; }

    /// <summary>The item's title, from <c>Title(...)</c>; null for the item's first <see cref="string"/> property.</summary>
    internal Func<object, string>? TitleOf { get; private protected set; }
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
}
