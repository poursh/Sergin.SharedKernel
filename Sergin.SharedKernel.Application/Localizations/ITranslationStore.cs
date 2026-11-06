namespace Sergin.SharedKernel.Application.Localizations;

/// <summary>
/// Where translations live. SharedKernel ships a store with no languages and no translations, so a host without a
/// translation module shows the English defaults; a module (Settings) supplies the real one.
/// </summary>
public interface ITranslationStore
{
    /// <summary>Every language the store holds, drafts included; callers decide what to offer.</summary>
    Task<IReadOnlyList<LanguageInfo>> GetLanguagesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The translations of one culture by key, whatever the language's status. An unknown culture answers an empty
    /// dictionary.
    /// </summary>
    Task<IReadOnlyDictionary<string, StoredTranslation>> GetTranslationsAsync(string culture, CancellationToken cancellationToken);
}
