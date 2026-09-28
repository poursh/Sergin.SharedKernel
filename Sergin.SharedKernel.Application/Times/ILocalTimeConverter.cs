namespace Sergin.SharedKernel.Application.Times;

/// <summary>
/// Converts a UTC <see cref="DateTime"/> into a specific <see cref="TimeZoneInfo"/> for display.
/// </summary>
/// <remarks>
/// Deliberately takes the target zone as a parameter rather than resolving one itself, so it stays
/// presentation-agnostic: a Blazor page resolves the viewer's zone through
/// <c>IUiTimeZoneStore</c> (browser-detected, falling back to the app-wide <c>Sergin:TimeZone</c> default);
/// a future WebApi host would resolve it its own way (e.g. a request header). This type only converts.
/// </remarks>
public interface ILocalTimeConverter
{
    /// <summary>Converts <paramref name="utcDateTime"/> into <paramref name="zone"/>.</summary>
    DateTimeOffset ToLocal(DateTime utcDateTime, TimeZoneInfo zone);
}
