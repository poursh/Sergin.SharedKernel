namespace Sergin.SharedKernel.Application.Localizations;

public static class LocalizerExtensions
{
    /// <summary>
    /// The localized text for <paramref name="key"/>, or <paramref name="fallback"/> when no resource exists for it.
    /// <see cref="ILocalizer"/> answers the key itself for a missing resource, which is no text to show a person.
    /// </summary>
    public static string LocalizedOrDefault(this ILocalizer localizer, string key, string fallback)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        Microsoft.Extensions.Localization.LocalizedString text = localizer[key];

        return text.ResourceNotFound ? fallback : text.Value;
    }
}
