using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Infrastructure.Localizations;

/// <summary>
/// One frozen dictionary per loaded culture, replaced whole on refresh, so a lookup never sees a half-loaded culture
/// and never waits for the store. Every culture the store answers for is cached, drafts included: which cultures a
/// user may select is the culture provider's decision, not the cache's.
/// </summary>
internal sealed class TranslationCache(ITranslationStore store) : ITranslationCache
{
    private readonly ConcurrentDictionary<string, FrozenDictionary<string, StoredTranslation>> cultures =
        new(StringComparer.OrdinalIgnoreCase);

    private volatile IReadOnlyList<LanguageInfo>? languages;

    public IReadOnlyList<LanguageInfo> Languages => languages ?? [];

    public async Task EnsureLoadedAsync(string culture, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);

        if (languages is null)
        {
            await RefreshLanguagesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!cultures.ContainsKey(culture))
        {
            await RefreshAsync(culture, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RefreshAsync(string culture, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);

        IReadOnlyDictionary<string, StoredTranslation> loaded =
            await store.GetTranslationsAsync(culture, cancellationToken).ConfigureAwait(false);

        cultures[culture] = loaded.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public async Task RefreshLanguagesAsync(CancellationToken cancellationToken)
    {
        languages = await store.GetLanguagesAsync(cancellationToken).ConfigureAwait(false);
    }

    public bool TryGet(string culture, string key, [NotNullWhen(true)] out StoredTranslation? translation)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(key);

        if (cultures.TryGetValue(culture, out FrozenDictionary<string, StoredTranslation>? byKey))
        {
            return byKey.TryGetValue(key, out translation);
        }

        translation = null;
        return false;
    }
}
