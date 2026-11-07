using System.Globalization;

namespace Sergin.SharedKernel.Presentation.Blazor.Localizations;

/// <summary>One language the switcher offers: its culture and the name it shows, in that language itself.</summary>
internal sealed record LanguageChoice(string Culture, string Label);

/// <summary>
/// What <see cref="SerginLanguageSwitcher"/> lists, kept apart from the component because a menu renders its items only
/// while it is open, so the prerendered page no longer shows them: the default language first, then every published
/// one, then the drafts the caller may preview, each marked with the draft suffix.
/// </summary>
internal static class LanguageChoices
{
    internal static IReadOnlyList<LanguageChoice> Build(
        string defaultCulture,
        IEnumerable<LanguageInfo> languages,
        bool canPreview,
        string draftSuffix)
    {
        List<LanguageChoice> choices =
        [
            new(defaultCulture, CultureInfo.GetCultureInfo(defaultCulture).NativeName),
        ];

        foreach (LanguageInfo language in languages)
        {
            if (string.Equals(language.Culture, defaultCulture, StringComparison.OrdinalIgnoreCase)
                || !language.IsPublished && !canPreview)
            {
                continue;
            }

            string name = language.IsPublished
                ? language.DisplayName
                : $"{language.DisplayName} ({draftSuffix})";

            choices.Add(new LanguageChoice(language.Culture, name));
        }

        return choices;
    }
}
