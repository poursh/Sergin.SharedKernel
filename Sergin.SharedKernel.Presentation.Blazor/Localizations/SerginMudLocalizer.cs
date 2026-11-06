using Microsoft.Extensions.Localization;
using MudBlazor;

namespace Sergin.SharedKernel.Presentation.Blazor.Localizations;

/// <summary>
/// Answers MudBlazor's own text lookups from the text catalog (<c>common.mud-blazor.*</c>), so an administrator's
/// translation reaches the pager, the data-grid filter and the date picker. A key the catalog does not know is
/// reported not found, which lets MudBlazor use its built-in text.
/// </summary>
public sealed class SerginMudLocalizer(IStringLocalizer localizer) : MudLocalizer
{
    public override LocalizedString this[string key]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(key);

            string catalogKey;

            try
            {
                catalogKey = MudBlazorTexts.CatalogKeyOf(key);
            }
            catch (ArgumentException)
            {
                return new LocalizedString(key, key, resourceNotFound: true);
            }

            LocalizedString found = localizer[catalogKey];

            return found.ResourceNotFound ? new LocalizedString(key, key, resourceNotFound: true) : new LocalizedString(key, found.Value, resourceNotFound: false);
        }
    }

    public override LocalizedString this[string key, params object[] arguments]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(arguments);

            LocalizedString found = this[key];

            return found.ResourceNotFound || arguments.Length == 0
                ? found
                : new LocalizedString(
                    key,
                    Application.Localizations.TextPlaceholders.Format(
                        found.Value, MudBlazorTexts.NamedArguments(arguments), System.Globalization.CultureInfo.CurrentCulture),
                    resourceNotFound: false);
        }
    }
}
