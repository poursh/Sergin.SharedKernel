using Sergin.SharedKernel.Domain.Localizations;

namespace Sergin.SharedKernel.Application.Localizations;

/// <summary>
/// A plain string carried where a <see cref="TextDefinition"/> is expected, for the shared components' string-typed
/// overloads that modules still call while their texts are being declared. It is shown exactly as written — never
/// looked up, never formatted — which is what those strings always rendered as, since none of them is a declared key.
/// Not in the catalog. Transitional: it goes with the string overloads once every module passes declared texts.
/// </summary>
public static class TransitionalText
{
    private static readonly TextKey key = TextKey.Create("common.transitional-text");

    /// <summary>A definition that shows <paramref name="text"/> as written.</summary>
    public static TextDefinition Of(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return new TextDefinition(key, text);
    }

    /// <summary>Whether <paramref name="text"/> came from <see cref="Of"/>.</summary>
    public static bool Is(TextDefinition text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return ReferenceEquals(text.Key, key);
    }
}
