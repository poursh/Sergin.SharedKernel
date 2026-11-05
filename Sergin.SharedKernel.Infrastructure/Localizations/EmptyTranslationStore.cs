using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Infrastructure.Localizations;

/// <summary>
/// The store of a host without a translation module: no languages and no translations, so every text shows its
/// English default.
/// </summary>
internal sealed class EmptyTranslationStore : ITranslationStore
{
    private static readonly IReadOnlyDictionary<string, StoredTranslation> none =
        new Dictionary<string, StoredTranslation>(StringComparer.Ordinal);

    public Task<IReadOnlyList<LanguageInfo>> GetLanguagesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<LanguageInfo>>([]);

    public Task<IReadOnlyDictionary<string, StoredTranslation>> GetTranslationsAsync(string culture, CancellationToken cancellationToken) =>
        Task.FromResult(none);
}
