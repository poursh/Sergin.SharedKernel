using System.Globalization;
using Microsoft.Extensions.Localization;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Infrastructure.Localizations;
internal sealed class DefaultLocalizer : ILocalizer
{
    // No resource backs this localizer: answering the key is a fallback, so say the resource was not found and
    // let a caller that wants a better fallback than the key (LocalizerExtensions.LocalizedOrDefault) have it.
    public LocalizedString this[string name] => new(name, name, resourceNotFound: true);

    public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: true);

    public CultureInfo CurrentCulture => Thread.CurrentThread.CurrentCulture;
}
