namespace Sergin.SharedKernel.Presentation;

/// <summary>
/// The application-wide fallback timezone, bound from the <c>Sergin</c> configuration section.
/// </summary>
/// <remarks>
/// This is a fallback only — a Blazor viewer's own browser-detected zone wins whenever it is known
/// (see <c>IUiTimeZoneStore</c> in <c>Sergin.SharedKernel.Presentation.Blazor</c>). This value applies
/// before the first render (no JS interop yet) or when the browser's reported zone doesn't resolve.
/// It lives in the presentation-agnostic project, next to <see cref="SerginApplicationOptions"/>, so a
/// future WebApi host can fall back to it the same way.
/// </remarks>
public sealed class SerginTimeZoneOptions
{
    /// <summary>The configuration key, relative to the <c>Sergin</c> section.</summary>
    public const string TimeZoneKey = "TimeZone";

    /// <summary>
    /// A <see cref="TimeZoneInfo"/> id (IANA, e.g. <c>"Asia/Tehran"</c>, or Windows, e.g.
    /// <c>"Iran Standard Time"</c> — .NET 10 resolves both cross-platform). Defaults to <c>"UTC"</c>.
    /// Named to match the <c>Sergin:TimeZone</c> configuration key exactly, since configuration binding
    /// matches by property name.
    /// </summary>
    public string TimeZone { get; set; } = "UTC";

    /// <summary>
    /// Validates the bound value, producing a message that names the offending key rather than the
    /// generic text an <c>OptionsBuilder.Validate(predicate)</c> lambda would emit.
    /// </summary>
    public bool Validate(out string failure)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            failure = $"Sergin:{TimeZoneKey} is '{TimeZone}', which is not a recognized timezone id: {ex.Message}";

            return false;
        }

        failure = string.Empty;

        return true;
    }
}
