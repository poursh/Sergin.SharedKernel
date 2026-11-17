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
/// <param name="IconOf">Reads an item's icon (<c>Icon(...)</c>); null when the configuration named none, meaning no icon.</param>
/// <param name="FilterPlaceholder">The filter box's placeholder: <c>Filter(...)</c>, else the shell's.</param>
/// <param name="Empty">What the tree shows with no items: <c>Empty(...)</c>, else the shell's.</param>
/// <param name="PromptTitle">The page's title while no item is selected: <c>Prompt(...)</c>, else the shell's.</param>
/// <param name="PromptHint">The prompt's hint line, or null for none.</param>
/// <param name="PromptIcon">The prompt's icon: <c>Prompt(...)</c>, else the tree icon.</param>
/// <param name="ChildrenTitle">The heading of an item's children list: <c>Children(...)</c>, else the shell's.</param>
/// <param name="ChildrenEmpty">What that list shows with no children: <c>Children(...)</c>, else the shell's.</param>
public sealed record TreeView(
    ViewRequest<Func<object>> Query,
    Type Item,
    Func<object, Guid> IdOf,
    Func<object, Guid?> ParentIdOf,
    Func<object, string> TitleOf,
    Func<object, string?>? IconOf,
    TextDefinition FilterPlaceholder,
    TextDefinition Empty,
    TextDefinition PromptTitle,
    TextDefinition? PromptHint,
    string PromptIcon,
    TextDefinition ChildrenTitle,
    TextDefinition ChildrenEmpty);
