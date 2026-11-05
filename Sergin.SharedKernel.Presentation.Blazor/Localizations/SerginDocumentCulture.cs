using System.Globalization;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Localizations;

/// <summary>
/// The current UI culture as the document needs it: the <c>lang</c> and <c>dir</c> of the root element and the
/// direction MudBlazor lays out in. The direction is the stored language's (which carries any administrator override),
/// falling back to what the culture itself says for one the store does not list.
/// </summary>
public sealed class SerginDocumentCulture(ITranslationCache cache)
{
    /// <summary>The current UI culture's name.</summary>
    public static string Lang => CultureInfo.CurrentUICulture.Name;

    /// <summary><c>rtl</c> or <c>ltr</c>.</summary>
    public string Dir => IsRightToLeft ? "rtl" : "ltr";

    /// <summary>Whether the current UI culture is laid out right to left.</summary>
    public bool IsRightToLeft
    {
        get
        {
            CultureInfo culture = CultureInfo.CurrentUICulture;
            LanguageInfo? language = cache.Languages.FirstOrDefault(
                candidate => string.Equals(candidate.Culture, culture.Name, StringComparison.OrdinalIgnoreCase));

            return language?.IsRightToLeft ?? culture.TextInfo.IsRightToLeft;
        }
    }
}
