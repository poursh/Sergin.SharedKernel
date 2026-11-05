using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Infrastructure.Localizations;

/// <summary>
/// One frozen dictionary per loaded culture, replaced whole on refresh, so a lookup never sees a half-loaded culture
/// and never waits for the store. Loads of one culture run one at a time, so a slow earlier read can never replace
/// the data a later refresh wrote. Loading a culture loads its parents too (<c>fa-IR</c> loads <c>fa</c>), since the
/// synchronous lookup falls back to them. Every culture the store answers for is cached, drafts included: which
/// cultures a user may select is the culture provider's decision, not the cache's.
/// </summary>
internal sealed class TranslationCache(ITranslationStore store) : ITranslationCache, IDisposable
{
    private readonly ConcurrentDictionary<string, FrozenDictionary<string, StoredTranslation>> cultures =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, SemaphoreSlim> gates = new(StringComparer.OrdinalIgnoreCase);

    private volatile IReadOnlyList<LanguageInfo>? languages;

    public IReadOnlyList<LanguageInfo> Languages => languages ?? [];

    public async Task EnsureLoadedAsync(string culture, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);

        if (languages is null)
        {
            await RefreshLanguagesAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (string name in ChainOf(culture))
        {
            await LoadAsync(name, onlyIfMissing: true, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RefreshAsync(string culture, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);

        await LoadAsync(culture, onlyIfMissing: false, cancellationToken).ConfigureAwait(false);

        foreach (string parent in ChainOf(culture).Skip(1))
        {
            await LoadAsync(parent, onlyIfMissing: true, cancellationToken).ConfigureAwait(false);
        }
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

    public void Dispose()
    {
        foreach (SemaphoreSlim gate in gates.Values)
        {
            gate.Dispose();
        }
    }

    /// <summary><paramref name="culture"/> and each parent up to, not including, the invariant culture.</summary>
    private static List<string> ChainOf(string culture)
    {
        CultureInfo info;

        try
        {
            info = CultureInfo.GetCultureInfo(culture);
        }
        catch (CultureNotFoundException)
        {
            return [culture];
        }

        List<string> chain = [];

        for (CultureInfo current = info; !string.IsNullOrEmpty(current.Name); current = current.Parent)
        {
            chain.Add(current.Name);
        }

        return chain.Count == 0 ? [culture] : chain;
    }

    private async Task LoadAsync(string culture, bool onlyIfMissing, CancellationToken cancellationToken)
    {
        if (onlyIfMissing && cultures.ContainsKey(culture))
        {
            return;
        }

        SemaphoreSlim gate = gates.GetOrAdd(culture, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Re-checked under the gate: the load this one waited behind may have filled it.
            if (onlyIfMissing && cultures.ContainsKey(culture))
            {
                return;
            }

            IReadOnlyDictionary<string, StoredTranslation> loaded =
                await store.GetTranslationsAsync(culture, cancellationToken).ConfigureAwait(false);

            cultures[culture] = loaded.ToFrozenDictionary(StringComparer.Ordinal);
        }
        finally
        {
            gate.Release();
        }
    }
}
