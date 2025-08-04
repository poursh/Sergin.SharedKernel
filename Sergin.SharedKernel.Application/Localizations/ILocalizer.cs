using System.Globalization;
using Microsoft.Extensions.Localization;

namespace Sergin.SharedKernel.Application.Localizations;
public interface ILocalizer
{
    /// <summary>
    /// Gets the string resource with the given name.
    /// </summary>
    /// <param name="name">The name of the string resource.</param>
    /// <returns>The string resource as a <see cref="LocalizedString"/>.</returns>
    LocalizedString this[string name] { get; }

    /// <summary>
    /// Gets the string resource with the given name and formatted with the supplied arguments.
    /// </summary>
    /// <param name="name">The name of the string resource.</param>
    /// <param name="arguments">The values to format the string with.</param>
    /// <returns>The formatted string resource as a <see cref="LocalizedString"/>.</returns>
    LocalizedString this[string name, params object[] arguments] { get; }

    CultureInfo CurrentCulture { get; }
}
