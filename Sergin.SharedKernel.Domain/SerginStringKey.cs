using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Sergin.SharedKernel.Domain;

/// <summary>
/// A string key made of dot-separated kebab-case segments, e.g. <c>permission.dm.devices.read</c>. The base holds
/// the rules every such key shares: PascalCase input is converted to kebab case and lowercased
/// (<c>PlatformAll</c> becomes <c>platform-all</c>), and every segment is lowercase letters, with single hyphens
/// between words. A derived key adds its own rules through <see cref="SerginStringKeyRules"/>: its first segment,
/// how many segments it needs, and its maximum length.
/// </summary>
/// <remarks>
/// Equality compares the key type as well as the value, so two key types holding the same text are never equal.
/// </remarks>
public abstract partial record SerginStringKey
{
    public const char Separator = '.';
    public const string SegmentPattern = "[a-z]+(-[a-z]+)*";
    public const string Format = $@"^{SegmentPattern}(\.{SegmentPattern})*$";

    protected SerginStringKey(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public IReadOnlyList<string> Segments => Value.Split(Separator);

    public static implicit operator string(SerginStringKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return key.Value;
    }

    public sealed override string ToString() => Value;

    /// <summary>
    /// Converts <paramref name="value"/> to kebab case and checks it against the shared rules and
    /// <paramref name="rules"/>. Throws <see cref="ArgumentException"/> naming <paramref name="keyName"/> when it
    /// does not pass.
    /// </summary>
    protected static string Normalize(string value, SerginStringKeyRules rules, string keyName)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Guard.Against.NullOrEmpty(value);

        string normalized = WordBoundary().Replace(value, "$1-$2").ToLowerInvariant();

        Guard.Against.InvalidFormat(normalized, keyName, Format);
        Guard.Against.StringTooLong(normalized, rules.MaxLength);

        string[] segments = normalized.Split(Separator);

        if (rules.FirstSegment is not null && segments[0] != rules.FirstSegment)
        {
            throw new ArgumentException($"{keyName} '{normalized}' must start with '{rules.FirstSegment}{Separator}'.", keyName);
        }

        if (segments.Length < rules.MinSegments)
        {
            throw new ArgumentException(
                $"{keyName} '{normalized}' has {segments.Length} segments; it needs at least {rules.MinSegments}.", keyName);
        }

        return normalized;
    }

    /// <summary>
    /// The non-throwing form of a derived key's <c>Create</c>, for values that arrive from outside the code base — a
    /// configuration key, a claim stamped by an earlier release, a row in another module's table — where an
    /// unparseable value is data to reject, not a bug to surface as an exception.
    /// </summary>
    protected static bool TryCreate<TKey>(string? value, Func<string, TKey> create, [NotNullWhen(true)] out TKey? key)
        where TKey : SerginStringKey
    {
        ArgumentNullException.ThrowIfNull(create);

        if (string.IsNullOrWhiteSpace(value))
        {
            key = null;
            return false;
        }

        try
        {
            key = create(value);
            return true;
        }
        catch (ArgumentException)
        {
            key = null;
            return false;
        }
    }

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex WordBoundary();
}

/// <summary>
/// The rules one key type adds to <see cref="SerginStringKey"/>'s shared ones. <paramref name="FirstSegment"/> is
/// the segment every key of the type starts with, or <see langword="null"/> for none.
/// </summary>
public sealed record SerginStringKeyRules(string? FirstSegment, int MinSegments, int MaxLength);
