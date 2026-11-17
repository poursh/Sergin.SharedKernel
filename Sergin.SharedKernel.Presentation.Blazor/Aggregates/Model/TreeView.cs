namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// A root's tree as a generated view reads it: the <c>ITreeQuery</c> (built with no arguments, answering a collection of
/// <paramref name="Item"/>) and the compiled readers of each item's id, parent id (null for a top-level item) and title.
/// </summary>
/// <param name="Query">The tree query, its invoker and its build.</param>
/// <param name="Item">The tree query's item type.</param>
/// <param name="IdOf">Reads an item's <c>Id</c>.</param>
/// <param name="ParentIdOf">Reads an item's <c>ParentId</c>; null for a top-level item.</param>
/// <param name="TitleOf">Reads an item's title: the configured <c>Tree&lt;T&gt;(tree =&gt; tree.Title(...))</c>, else its first <see cref="string"/> property.</param>
public sealed record TreeView(
    ViewRequest<Func<object>> Query,
    Type Item,
    Func<object, Guid> IdOf,
    Func<object, Guid?> ParentIdOf,
    Func<object, string> TitleOf);
