using System.Collections.Concurrent;
using System.Globalization;
using Sergin.SharedKernel.Application.Times;

namespace Sergin.SharedKernel.Presentation.Localizations;

/// <summary>
/// <see cref="ICultureFormatter"/> over <see cref="CultureInfo.CurrentCulture"/>. The time zone stays
/// <see cref="ILocalTimeConverter"/>'s job.
/// </summary>
/// <remarks>
/// Under ICU, .NET 10's <c>fa-IR</c> already defaults to the Persian calendar and groups with U+066C, but still renders
/// Latin digits, so the digit map is the formatter's. A <c>fa</c> culture that does not default to the calendar is
/// cloned with it set.
/// </remarks>
public sealed class CultureFormatter(ILocalTimeConverter timeConverter) : ICultureFormatter
{
    private const string PersianDate = "yyyy/MM/dd";
    private const string PersianDateTime = "yyyy/MM/dd HH:mm";
    private const string DateTimePattern = "dd MMM yyyy HH:mm";

    private static readonly ConcurrentDictionary<string, CultureInfo> PersianCultures = new(StringComparer.OrdinalIgnoreCase);

    public CultureInfo Culture
    {
        get
        {
            CultureInfo current = CultureInfo.CurrentCulture;

            return IsPersian(current) && current.DateTimeFormat.Calendar is not PersianCalendar
                ? PersianCultures.GetOrAdd(current.Name, _ => WithPersianCalendar(current))
                : current;
        }
    }

    public string Date(DateTimeOffset utc, TimeZoneInfo? zone = null) =>
        Digits(Local(utc, zone).ToString(IsPersian(Culture) ? PersianDate : "d", Culture));

    public string DateTime(DateTimeOffset utc, TimeZoneInfo? zone = null) =>
        Digits(Local(utc, zone).ToString(IsPersian(Culture) ? PersianDateTime : DateTimePattern, Culture));

    public string Number(long value) => Digits(value.ToString("N0", Culture));

    public string Number(decimal value, int decimals) =>
        Digits(value.ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), Culture));

    private DateTimeOffset Local(DateTimeOffset utc, TimeZoneInfo? zone) =>
        zone is null ? utc : timeConverter.ToLocal(utc.UtcDateTime, zone);

    private string Digits(string text) => IsPersian(Culture) ? MapDigits(text) : text;

    private static bool IsPersian(CultureInfo culture) => culture.TwoLetterISOLanguageName == "fa";

    private static string MapDigits(string text) =>
        string.Create(text.Length, text, static (span, source) =>
        {
            for (int i = 0; i < source.Length; i++)
            {
                span[i] = source[i] is >= '0' and <= '9' ? (char)('۰' + (source[i] - '0')) : source[i];
            }
        });

    private static CultureInfo WithPersianCalendar(CultureInfo source)
    {
        var clone = (CultureInfo)source.Clone();
        clone.DateTimeFormat.Calendar = new PersianCalendar();
        return clone;
    }
}
