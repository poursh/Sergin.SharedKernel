using System.Globalization;
using Microsoft.Extensions.Localization;

namespace Sergin.SharedKernel.Application.Localizations;

/// <summary>Joins a declared <see cref="TextDefinition"/> to an <see cref="IStringLocalizer"/> lookup.</summary>
public static class LocalizerTextExtensions
{
    private static readonly IReadOnlyDictionary<string, object?> noArguments = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>
    /// <paramref name="text"/> in the current UI culture, its named placeholders filled from
    /// <paramref name="arguments"/> — an anonymous object (<c>new { DeviceId = id }</c>) or an
    /// <see cref="IReadOnlyDictionary{TKey, TValue}"/>. A key the localizer does not know shows the declared default,
    /// never the key.
    /// </summary>
    public static string Text(this IStringLocalizer localizer, TextDefinition text, object? arguments = null)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(text);

        if (TransitionalText.Is(text))
        {
            return text.Default;
        }

        LocalizedString found = localizer[text.Key.Value];
        string template = found.ResourceNotFound ? text.Default : found.Value;

        return TextPlaceholders.Format(
            template,
            arguments is null ? noArguments : TextPlaceholders.ArgumentsOf(arguments),
            CultureInfo.CurrentCulture);
    }
}
