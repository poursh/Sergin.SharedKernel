namespace Sergin.SharedKernel.Application.Localizations;

/// <summary>
/// The texts of one form field or column, declared together: a label and, optionally, a hint and a placeholder.
/// Keys are <c>&lt;prefix&gt;.fields.&lt;property&gt;.label|hint|placeholder</c>.
/// </summary>
public sealed record FieldText(
    TextCategory Category,
    string PropertyName,
    TextDefinition Label,
    TextDefinition? Hint,
    TextDefinition? Placeholder);
