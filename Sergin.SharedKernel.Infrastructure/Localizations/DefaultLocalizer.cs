using System.Globalization;
using Microsoft.Extensions.Localization;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Infrastructure.Localizations;
internal sealed class DefaultLocalizer : ILocalizer
{
    public LocalizedString this[string name] => new (name, name);

    public LocalizedString this[string name, params object[] arguments] => new(name, name);

    public CultureInfo CurrentCulture => Thread.CurrentThread.CurrentCulture;
}
