namespace Sergin.SharedKernel.Application.Localizations;

/// <summary>
/// One stored translation: the translated text and the default text it was translated from. A source that differs
/// from the catalog's current default marks the translation as needing review; it is still shown.
/// </summary>
public sealed record StoredTranslation(string Text, string SourceText)
{
    /// <summary>The placeholder names <see cref="Text"/> uses, read once when the translation is built.</summary>
    public IReadOnlySet<string> Placeholders { get; } = TextPlaceholders.In(Text);

    // Equality is the stored values; the placeholder set is derived from Text and would otherwise compare by reference.
    public bool Equals(StoredTranslation? other) =>
        other is not null
        && string.Equals(Text, other.Text, StringComparison.Ordinal)
        && string.Equals(SourceText, other.SourceText, StringComparison.Ordinal);

    public override int GetHashCode() => HashCode.Combine(Text, SourceText);
}
