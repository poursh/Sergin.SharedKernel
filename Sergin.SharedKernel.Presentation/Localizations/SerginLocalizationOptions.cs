using System.Globalization;

namespace Sergin.SharedKernel.Presentation.Localizations;

/// <summary>
/// Localization settings, bound from <c>Sergin:Localization</c>. The default culture is what every request renders in
/// unless its caller chose another language that is offered to them; English unless configured.
/// </summary>
public sealed class SerginLocalizationOptions
{
    /// <summary>The configuration section, relative to <c>Sergin</c>.</summary>
    public const string SectionName = "Localization";

    /// <summary>A predefined culture name (<c>en</c>, <c>fa-IR</c>). Defaults to <c>en</c>.</summary>
    public string DefaultCulture { get; set; } = "en";

    /// <summary>
    /// Validates the bound value, producing a message that names the offending key rather than the generic text an
    /// <c>OptionsBuilder.Validate(predicate)</c> lambda would emit.
    /// </summary>
    public bool Validate(out string failure)
    {
        string key = $"Sergin:{SectionName}:{nameof(DefaultCulture)}";

        if (string.IsNullOrWhiteSpace(DefaultCulture))
        {
            failure = $"{key} is required: a culture name such as 'en'.";
            return false;
        }

        try
        {
            // Predefined only: ICU would otherwise build a culture for any well-formed name, typos included.
            CultureInfo.GetCultureInfo(DefaultCulture, predefinedOnly: true);
        }
        catch (CultureNotFoundException ex)
        {
            failure = $"{key} is '{DefaultCulture}', which is not a known culture: {ex.Message}";
            return false;
        }

        failure = string.Empty;
        return true;
    }
}
