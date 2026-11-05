namespace Sergin.SharedKernel.Application.Localizations;

/// <summary>
/// One stored translation: the translated text and the default text it was translated from. A source that differs
/// from the catalog's current default marks the translation as needing review; it is still shown.
/// </summary>
public sealed record StoredTranslation(string Text, string SourceText);
