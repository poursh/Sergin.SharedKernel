using System.Globalization;

namespace Sergin.SharedKernel.Presentation.Localizations;

/// <summary>
/// The one place a date or a number is turned into display text, reading the current culture (spec decisions 11 and 12).
/// Under a Persian culture dates are Jalali and digits are Persian; otherwise the culture's own formats apply. Only the
/// text this formatter returns is digit-mapped: ids, codes, versions and user data are shown as stored.
/// </summary>
public interface ICultureFormatter
{
    /// <summary>The current culture; for Persian, one whose calendar is the <see cref="PersianCalendar"/>.</summary>
    CultureInfo Culture { get; }

    /// <summary>The date of <paramref name="utc"/>, in <paramref name="zone"/> when given, otherwise as its own offset says.</summary>
    string Date(DateTimeOffset utc, TimeZoneInfo? zone = null);

    /// <summary>The date and minute of <paramref name="utc"/>, in <paramref name="zone"/> when given.</summary>
    string DateTime(DateTimeOffset utc, TimeZoneInfo? zone = null);

    /// <summary>A whole number with the culture's grouping.</summary>
    string Number(long value);

    /// <summary>A number with exactly <paramref name="decimals"/> fraction digits and the culture's separators.</summary>
    string Number(decimal value, int decimals);
}
