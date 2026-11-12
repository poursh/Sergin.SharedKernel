using System.Globalization;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

/// <summary>A field's value as a generated view shows it: a date in the viewer's culture, a count with its digits, anything else as the culture prints it.</summary>
internal static class FieldValueText
{
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
