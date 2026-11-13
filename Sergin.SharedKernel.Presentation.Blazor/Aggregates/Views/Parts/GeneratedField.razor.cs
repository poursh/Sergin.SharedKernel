using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

/// <summary>
/// One <see cref="FieldView"/> of a loaded record as a <see cref="Pages.SerginDetailField"/>: its configured component
/// (given <c>Context</c> and <c>Value</c>), a link when the field has an <c>Href</c>, else the value as text — a date in
/// the viewer's culture, anything else as the culture prints it. <see cref="FieldView.Get"/> answers a value object's
/// primitive already, so nothing here reads metadata.
/// </summary>
public sealed partial class GeneratedField
{
    [Parameter]
    [EditorRequired]
    public FieldView Field { get; set; } = default!;

    /// <summary>The loaded record, of the GetOne response's type.</summary>
    [Parameter]
    [EditorRequired]
    public object Record { get; set; } = default!;

    /// <summary>What a configured component receives besides the value.</summary>
    [Parameter]
    [EditorRequired]
    public AggregateViewContext Context { get; set; } = default!;

    [Inject]
    private ICultureFormatter Formatter { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    private object? Value => Field.Get(Record);

    private string? Href => Field.Href?.Invoke(Record);

    private Dictionary<string, object?> ComponentParameters => new(StringComparer.Ordinal)
    {
        ["Context"] = Context,
        ["Value"] = Value,
    };

    // The field reader already read a value object through to its primitive; Format(...) applies here as in a list row.
    private string? Text => FieldValueText.Render(Field, Value, Formatter, Localizer);
}
