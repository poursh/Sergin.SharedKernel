namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// One property of a GetOne response or list item as a generated view shows it. Only a property with a
/// <see cref="FieldText"/> in the root's category is a field; a <see cref="Guid"/> one, or one named <c>Id</c>, is
/// <see cref="Hidden"/> unless the configuration names it.
/// </summary>
/// <param name="Name">The property name.</param>
/// <param name="Label">Its declared texts.</param>
/// <param name="Get">Reads the value from a record of the response or item type, a value object read through to the primitive it wraps; compiled once.</param>
/// <param name="Href">Where the value links to, from <c>Link(...)</c>; null for plain text.</param>
/// <param name="Component">A component rendering the value instead (<c>[Parameter] Context</c>, <c>[Parameter] Value</c>); null for the default.</param>
/// <param name="Hidden">Not shown.</param>
/// <param name="Order">Its place: the declaration order unless the configuration set <c>Order(n)</c>.</param>
public sealed record FieldView(
    string Name,
    FieldText Label,
    Func<object, object?> Get,
    Func<object, string>? Href,
    Type? Component,
    bool Hidden,
    int Order);
