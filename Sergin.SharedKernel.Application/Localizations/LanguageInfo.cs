namespace Sergin.SharedKernel.Application.Localizations;

/// <summary>
/// A language the translation store knows: its culture code, its name in its own script, its direction, and whether
/// it is published (offered to users) or still a draft.
/// </summary>
public sealed record LanguageInfo(string Culture, string DisplayName, bool IsRightToLeft, bool IsPublished);
