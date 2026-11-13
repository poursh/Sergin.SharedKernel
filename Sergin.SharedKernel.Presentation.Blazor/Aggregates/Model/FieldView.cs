namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// One property of a GetOne response or list item as a generated view shows it. Only a property with a
/// <see cref="FieldText"/> in the root's category is a field; a <see cref="Guid"/> one, or one named <c>Id</c>, is
/// <see cref="Hidden"/> unless the configuration names it.
/// </summary>
/// <param name="Name">The property name.</param>
/// <param name="Label">Its label: its <see cref="FieldText"/>'s, or the one the configuration declared with <c>Label(...)</c>.</param>
/// <param name="Get">Reads the value from a record of the response or item type, a value object read through to the primitive it wraps; compiled once.</param>
/// <param name="Href">Where the value links to, from <c>Link(...)</c>; null for plain text.</param>
/// <param name="Component">A component rendering the value instead (<c>[Parameter] Context</c>, <c>[Parameter] Value</c>); null for the default.</param>
/// <param name="Hidden">Not shown.</param>
/// <param name="Order">Its place: the declaration order unless the configuration set <c>Order(n)</c>.</param>
/// <param name="Format">A text the value is rendered through (<c>Format(...)</c>), or null for the value as it is.</param>
/// <param name="FormatPlaceholder">The placeholder of <paramref name="Format"/> that receives the value.</param>
public sealed record FieldView(
    string Name,
    TextDefinition Label,
    Func<object, object?> Get,
    Func<object, string>? Href,
    Type? Component,
    bool Hidden,
    int Order,
    TextDefinition? Format = null,
    string FormatPlaceholder = "Value");
