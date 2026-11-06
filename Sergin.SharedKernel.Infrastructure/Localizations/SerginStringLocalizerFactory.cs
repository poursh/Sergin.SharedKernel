using System.Collections.Concurrent;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Infrastructure.Localizations;

/// <summary>
/// Keys are global, so every <see cref="IStringLocalizer{T}"/> and every <c>Create</c> answers the same localizer over
/// the one catalog and cache. A key the catalog does not know is logged once.
/// </summary>
internal sealed class SerginStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly ConcurrentDictionary<string, byte> reported = new(StringComparer.Ordinal);
    private readonly ILogger<SerginStringLocalizerFactory> logger;
    private readonly SerginStringLocalizer localizer;

    public SerginStringLocalizerFactory(TextCatalog catalog, ITranslationCache cache, ILogger<SerginStringLocalizerFactory> logger)
    {
        this.logger = logger;
        localizer = new SerginStringLocalizer(catalog, cache, ReportUnknown);
    }

    public IStringLocalizer Create(Type resourceSource) => localizer;

    public IStringLocalizer Create(string baseName, string location) => localizer;

    private void ReportUnknown(string key)
    {
        if (reported.TryAdd(key, 0))
        {
            logger.LogWarning("Text key {Key} is not declared in the text catalog.", key);
        }
    }
}
