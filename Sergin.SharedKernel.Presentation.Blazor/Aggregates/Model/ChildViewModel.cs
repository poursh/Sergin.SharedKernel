namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// Everything a generated view needs about one child entity of a root, joined once at start like the root's
/// <see cref="AggregateViewModel"/>. A request that takes the child by its ids takes the root's id first.
/// </summary>
/// <param name="Child">The child entity type.</param>
/// <param name="Singular">The child's name, from <c>Child&lt;T&gt;(child => child.Named(...))</c>.</param>
/// <param name="Title">The child list section's title: the configured <c>Title(...)</c>, else <paramref name="Singular"/>.</param>
/// <param name="Routes">Where its pages live.</param>
/// <param name="List">The <c>IChildListOf</c> query, its invoker and its build from the root's id and a page's paging, term, filters and sort, or null.</param>
/// <param name="ListItem">The list query's item type; null with no list.</param>
/// <param name="ListFields">The list item's fields, hidden ones included, in order.</param>
/// <param name="ListFieldSet">The list query's declared sort and filter fields, or null.</param>
/// <param name="ListTitleField">The list item's display property.</param>
/// <param name="ListSearchPlaceholder">The list's quick-search placeholder; null for the shell's.</param>
/// <param name="EmptyText">What the list says with no rows, from <c>Empty(...)</c>; null for the shell's.</param>
/// <param name="ListItemArguments">Reads a list item into a name-to-value bag: what fills a row command's texts. Null with no list.</param>
/// <param name="GetOne">The <c>IChildGetOneQuery</c>, its invoker and its build from the root's and the child's id, or null.</param>
/// <param name="DetailFields">The GetOne response's fields, hidden ones included, in order.</param>
/// <param name="DetailTitleField">The GetOne response's display property.</param>
/// <param name="DetailArguments">Reads a GetOne response into a name-to-value bag; null with no GetOne.</param>
/// <param name="Add">The <c>IAddChildCommand</c>, its invoker and its build from the root's id and form values, or null.</param>
/// <param name="AddFields">The add command's shown fields.</param>
/// <param name="AddTitle">The add form's title, from <c>Form&lt;T&gt;(form => form.Title(...))</c>; null for the shell's.</param>
/// <param name="Edit">
/// The child's one edit command (its <c>IUpdateCommand</c>, or the one command declared <c>Edit(SerginEditMode.Page)</c>),
/// its invoker and its build from the root's id, the child's id and form values; or null.
/// </param>
/// <param name="EditFields">The edit command's shown fields.</param>
/// <param name="EditTitle">The edit form's title; null for the shell's.</param>
/// <param name="EditCommand">The edit command as an action; null with no <paramref name="Edit"/>.</param>
/// <param name="Commands">Every command on the child that is an action, sorted by their request types' full names (ordinal).</param>
/// <param name="ListBodyType">The child list section's body closed over <paramref name="ListItem"/>, made at registry build; null with no list.</param>
public sealed record ChildViewModel(
    Type Child,
    TextDefinition Singular,
    TextDefinition Title,
    ChildViewRoutes Routes,
    ViewRequest<Func<Guid, ListQueryInput, object>>? List,
    Type? ListItem,
    IReadOnlyList<FieldView> ListFields,
    ListFieldSet? ListFieldSet,
    string? ListTitleField,
    TextDefinition? ListSearchPlaceholder,
    TextDefinition? EmptyText,
    Func<object, IReadOnlyDictionary<string, object?>>? ListItemArguments,
    ViewRequest<Func<Guid, Guid, object>>? GetOne,
    IReadOnlyList<FieldView> DetailFields,
    string? DetailTitleField,
    Func<object, IReadOnlyDictionary<string, object?>>? DetailArguments,
    ViewRequest<Func<Guid, IReadOnlyDictionary<string, object?>, object>>? Add,
    IReadOnlyList<FormFieldView> AddFields,
    TextDefinition? AddTitle,
    ViewRequest<Func<Guid, Guid, IReadOnlyDictionary<string, object?>, object>>? Edit,
    IReadOnlyList<FormFieldView> EditFields,
    TextDefinition? EditTitle,
    CommandView? EditCommand,
    IReadOnlyList<CommandView> Commands,
    Type? ListBodyType = null);
