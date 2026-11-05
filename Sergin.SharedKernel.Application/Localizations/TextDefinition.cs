using Sergin.SharedKernel.Domain.Localizations;

namespace Sergin.SharedKernel.Application.Localizations;

/// <summary>
/// One declared piece of text: its key, the English default that ships in code, and an optional note for the
/// translator.
/// </summary>
public sealed record TextDefinition(TextKey Key, string Default, string? Description = null)
{
    /// <summary>The placeholder names <see cref="Default"/> uses.</summary>
    public IReadOnlySet<string> Placeholders => TextPlaceholders.In(Default);
}
