namespace Sergin.SharedKernel.Application.Localizations;

/// <summary>What <see cref="TextSource"/> and <see cref="TextCatalog"/> read from an <see cref="EnumTexts{TEnum}"/> without knowing its enum.</summary>
public interface IEnumTexts
{
    TextCategory Category { get; }

    Type EnumType { get; }

    /// <summary>The declared text of each member, keyed by the boxed member.</summary>
    IReadOnlyDictionary<object, TextDefinition> Members { get; }
}

/// <summary>
/// The declared texts of an enum's members, one per member, under the category that declared them
/// (<c>&lt;category&gt;.values.&lt;enum&gt;.&lt;member&gt;</c>). Declare with <see cref="TextCategory.Values{TEnum}"/>.
/// </summary>
public sealed class EnumTexts<TEnum> : IEnumTexts
    where TEnum : struct, Enum
{
    private readonly Dictionary<object, TextDefinition> boxed;

    internal EnumTexts(TextCategory category, IReadOnlyDictionary<TEnum, TextDefinition> all)
    {
        Category = category;
        All = all;
        boxed = all.ToDictionary(pair => (object)pair.Key, pair => pair.Value);
    }

    public TextCategory Category { get; }

    /// <summary>The declared text of each declared member.</summary>
    public IReadOnlyDictionary<TEnum, TextDefinition> All { get; }

    Type IEnumTexts.EnumType => typeof(TEnum);

    IReadOnlyDictionary<object, TextDefinition> IEnumTexts.Members => boxed;

    /// <summary>The text of <paramref name="value"/>; an undeclared member throws <see cref="ArgumentOutOfRangeException"/>.</summary>
    public TextDefinition For(TEnum value) =>
        All.TryGetValue(value, out TextDefinition? text)
            ? text
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"{typeof(TEnum).Name}.{value} has no declared text in '{Category.Prefix}'.");
}
