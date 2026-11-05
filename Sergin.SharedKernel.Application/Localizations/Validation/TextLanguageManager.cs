using System.Globalization;
using FluentValidation.Resources;
using Microsoft.Extensions.Localization;
using Sergin.SharedKernel.Application.Localizations.Common;

namespace Sergin.SharedKernel.Application.Localizations.Validation;

/// <summary>
/// FluentValidation's message templates from the text catalog: a built-in validator's resource name
/// (<c>NotEmptyValidator</c>) answers its <see cref="CommonValidationTexts"/> key in the requested culture, through the
/// host's <see cref="IStringLocalizer"/> (translation, parent culture's, then the English default). Only the template:
/// FluentValidation still fills its placeholders. A resource name not declared there answers FluentValidation's own
/// English, so nothing it adds in a later version shows blank.
/// </summary>
public sealed class TextLanguageManager(IStringLocalizer localizer) : ILanguageManager
{
    private static readonly LanguageManager english = new() { Enabled = false };

    /// <summary>When false, every template is the English default, as for FluentValidation's own manager.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>A culture every lookup uses instead of the current UI culture; null (the default) for the current one.</summary>
    public CultureInfo? Culture { get; set; }

    public string GetString(string key, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(key);

        TextDefinition? definition = CommonValidationTexts.ForValidator(key);

        if (definition is null)
        {
            return english.GetString(key);
        }

        return Enabled ? Template(definition, culture ?? Culture) : definition.Default;
    }

    /// <summary><paramref name="definition"/>'s template in <paramref name="culture"/> (the current UI culture when null), unformatted.</summary>
    public string Template(TextDefinition definition, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        CultureInfo current = CultureInfo.CurrentUICulture;

        if (culture is null || culture.Equals(current))
        {
            return Find(definition);
        }

        // The localizer reads the current UI culture; switch it for this one lookup.
        CultureInfo.CurrentUICulture = culture;

        try
        {
            return Find(definition);
        }
        finally
        {
            CultureInfo.CurrentUICulture = current;
        }
    }

    private string Find(TextDefinition definition)
    {
        LocalizedString found = localizer[definition.Key.Value];

        return found.ResourceNotFound ? definition.Default : found.Value;
    }
}
