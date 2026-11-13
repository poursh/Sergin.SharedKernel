using System.Globalization;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

/// <summary>A field's value as a generated view shows it: a date in the viewer's culture, a count with its digits, anything else as the culture prints it.</summary>
internal static class FieldValueText
{
    /// <summary>
    /// A field's value as a row or record shows it: through the text the configuration gave with <c>Format(...)</c> (the
    /// value, as <see cref="Format(object?, ICultureFormatter)"/> prints it, in the placeholder it names), else as it is.
    /// </summary>
    public static string? Render(FieldView field, object? value, ICultureFormatter formatter, IStringLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(localizer);

        string? text = Format(value, formatter);

        return field.Format is { } format && text is not null
            ? localizer.Text(format, new Dictionary<string, object?>(StringComparer.Ordinal) { [field.FormatPlaceholder] = text })
            : text;
    }

    public static string? Format(object? value, ICultureFormatter formatter) => value switch
    {
        null => null,
        string text => text,
        DateTimeOffset moment => formatter.DateTime(moment),
        DateTime moment => formatter.DateTime(new DateTimeOffset(DateTime.SpecifyKind(moment, DateTimeKind.Utc))),
        int number => formatter.Number(number),
        long number => formatter.Number(number),
        IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentCulture),
        _ => value.ToString(),
    };
}
