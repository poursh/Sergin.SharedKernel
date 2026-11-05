using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Localization;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Infrastructure.Localizations;

/// <summary>
/// Looks a key up under <see cref="CultureInfo.CurrentUICulture"/>, read per call: the culture's translation, then
/// each parent culture's (<c>fa-IR</c> to <c>fa</c>), skipping any translation whose placeholders no longer match the
/// default's; then the catalog's default; and, for a key the catalog does not know, the key itself, marked not found.
/// </summary>
internal sealed class SerginStringLocalizer(TextCatalog catalog, ITranslationCache cache, Action<string> unknownKey) : IStringLocalizer
{
    // Each default's placeholder set, read once per key rather than on every lookup.
    private readonly ConcurrentDictionary<string, IReadOnlySet<string>> defaultPlaceholders = new(StringComparer.Ordinal);

    public LocalizedString this[string name]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(name);

            return Find(name, CultureInfo.CurrentUICulture);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(arguments);

            LocalizedString found = Find(name, CultureInfo.CurrentUICulture);

            if (arguments.Length == 0 || found.ResourceNotFound)
            {
                return found;
            }

            if (arguments.Length > 1)
            {
                throw new ArgumentException(
                    "Placeholders are named: pass one object holding them (new { DeviceId = id }), not positional values.",
                    nameof(arguments));
            }

            string formatted = TextPlaceholders.Format(
                found.Value, TextPlaceholders.ArgumentsOf(arguments[0]), CultureInfo.CurrentCulture);

            return new LocalizedString(name, formatted, resourceNotFound: false, found.SearchedLocation);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        CultureInfo culture = CultureInfo.CurrentUICulture;

        foreach (TextCatalogEntry entry in catalog.Entries)
        {
            TextDefinition definition = entry.Definition;

            yield return new LocalizedString(
                definition.Key.Value,
                Translate(definition, culture, includeParentCultures) ?? definition.Default,
                resourceNotFound: false);
        }
    }

    private LocalizedString Find(string name, CultureInfo culture)
    {
        if (!catalog.TryGet(name, out TextDefinition? definition))
        {
            unknownKey(name);
            return new LocalizedString(name, name, resourceNotFound: true);
        }

        return new LocalizedString(
            name, Translate(definition, culture, includeParentCultures: true) ?? definition.Default, resourceNotFound: false);
    }

    private string? Translate(TextDefinition definition, CultureInfo culture, bool includeParentCultures)
    {
        IReadOnlySet<string> placeholders = defaultPlaceholders.GetOrAdd(definition.Key.Value, _ => definition.Placeholders);

        for (CultureInfo current = culture; !string.IsNullOrEmpty(current.Name); current = current.Parent)
        {
            if (cache.TryGet(current.Name, definition.Key.Value, out StoredTranslation? translation)
                && placeholders.SetEquals(translation.Placeholders))
            {
                return translation.Text;
            }

            if (!includeParentCultures)
            {
                break;
            }
        }

        return null;
    }
}
