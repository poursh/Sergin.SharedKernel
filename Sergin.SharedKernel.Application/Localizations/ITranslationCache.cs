using System.Diagnostics.CodeAnalysis;

namespace Sergin.SharedKernel.Application.Localizations;

/// <summary>
/// The translations held in memory, loaded from <see cref="ITranslationStore"/> one culture at a time. Lookups are
/// synchronous, so a culture must be loaded before it is shown; a refresh replaces a culture's entry in one swap.
/// </summary>
public interface ITranslationCache
{
    /// <summary>Every language the store holds, once <see cref="EnsureLoadedAsync"/> or <see cref="RefreshLanguagesAsync"/> has run; empty before.</summary>
    IReadOnlyList<LanguageInfo> Languages { get; }

    /// <summary>Loads the languages and <paramref name="culture"/>'s translations, unless already loaded.</summary>
    Task EnsureLoadedAsync(string culture, CancellationToken cancellationToken);

    /// <summary>Reloads <paramref name="culture"/>'s translations from the store.</summary>
    Task RefreshAsync(string culture, CancellationToken cancellationToken);

    /// <summary>
    /// Reloads the language list from the store. Refreshes run one at a time, so a refresh requested after a save reads
    /// after any earlier read has written.
    /// </summary>
    Task RefreshLanguagesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Forgets the language list, so the next <see cref="EnsureLoadedAsync"/> reloads it; <see cref="Languages"/> is empty
    /// until then. For a writer whose refresh after commit failed: a stale list must not outlive the failure.
    /// </summary>
    void InvalidateLanguages();

    /// <summary>The translation of <paramref name="key"/> in <paramref name="culture"/>; false when none, or when the culture is not loaded.</summary>
    bool TryGet(string culture, string key, [NotNullWhen(true)] out StoredTranslation? translation);
}
