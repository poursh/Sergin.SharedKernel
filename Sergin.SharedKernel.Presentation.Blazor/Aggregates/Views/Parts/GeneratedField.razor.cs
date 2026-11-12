using System.Globalization;
using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

/// <summary>
/// One <see cref="FieldView"/> of a loaded record as a <see cref="Pages.SerginDetailField"/>: its configured component
/// (given <c>Context</c> and <c>Value</c>), a link when the field has an <c>Href</c>, else the value as text — a value
/// object read through to the value it wraps, a date in the viewer's culture, anything else as the culture prints it.
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

    private object? Value => Field.Get(Record);

    private string? Href => Field.Href?.Invoke(Record);

    private Dictionary<string, object?> ComponentParameters => new(StringComparer.Ordinal)
    {
        ["Context"] = Context,
        ["Value"] = Value,
    };

    private string? Text => Format(ValueObjects.Unwrap(Value));

    private string? Format(object? value) => value switch
    {
        null => null,
        string text => text,
        DateTimeOffset moment => Formatter.DateTime(moment),
        DateTime moment => Formatter.DateTime(new DateTimeOffset(DateTime.SpecifyKind(moment, DateTimeKind.Utc))),
        int number => Formatter.Number(number),
        long number => Formatter.Number(number),
        IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentCulture),
        _ => value.ToString(),
    };
}
