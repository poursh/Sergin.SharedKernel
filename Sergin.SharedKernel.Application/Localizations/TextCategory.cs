using Sergin.SharedKernel.Domain.Localizations;

namespace Sergin.SharedKernel.Application.Localizations;

/// <summary>
/// A named group of declared texts under one module, e.g. <c>device-management.devices</c>. Segment names may be
/// written PascalCase; keys always come out kebab case.
/// </summary>
public sealed class TextCategory
{
    private const string FieldsSegment = "fields";
    private const string ValuesSegment = "values";

    private TextCategory(string module, IReadOnlyList<string> path)
    {
        Module = module;
        Path = path;
        Prefix = string.Join(SerginStringKey.Separator, [module, .. path]);
    }

    public string Module { get; }

    public IReadOnlyList<string> Path { get; }

    public string Prefix { get; }

    public static TextCategory Create(string module, params string[] path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return new TextCategory(SerginStringKey.ToSegment(module), [.. path.Select(SerginStringKey.ToSegment)]);
    }

    public TextCategory Child(string name) => new(Module, [.. Path, SerginStringKey.ToSegment(name)]);

    public TextDefinition Text(string name, string @default, string? description = null) =>
        new(KeyOf(SerginStringKey.ToSegment(name)), @default, description);

    /// <summary>
    /// Declares the texts of <typeparamref name="TEnum"/>'s members, keyed <c>&lt;prefix&gt;.values.&lt;enum&gt;.&lt;member&gt;</c>.
    /// A member declared twice is refused; leaving a member out is allowed here and refused by the field-text guard
    /// when a configured request shows the enum.
    /// </summary>
    public EnumTexts<TEnum> Values<TEnum>(params (TEnum Value, string Default, string? Description)[] values)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(values);

        string enumSegment = SerginStringKey.ToSegment(typeof(TEnum).Name);
        Dictionary<TEnum, TextDefinition> declared = [];

        foreach ((TEnum value, string @default, string? description) in values)
        {
            if (declared.ContainsKey(value))
            {
                throw new ArgumentException($"{typeof(TEnum).Name}.{value} is declared twice in '{Prefix}'.", nameof(values));
            }

            string member = SerginStringKey.ToSegment(value.ToString());

            declared.Add(
                value,
                new TextDefinition(
                    KeyOf($"{ValuesSegment}{SerginStringKey.Separator}{enumSegment}{SerginStringKey.Separator}{member}"),
                    @default,
                    description));
        }

        return new EnumTexts<TEnum>(this, declared);
    }

    public FieldText Field(string propertyName, string label, string? hint = null, string? placeholder = null)
    {
        string field = $"{FieldsSegment}{SerginStringKey.Separator}{SerginStringKey.ToSegment(propertyName)}";

        return new FieldText(
            this,
            propertyName,
            new TextDefinition(KeyOf($"{field}.label"), label),
            hint is null ? null : new TextDefinition(KeyOf($"{field}.hint"), hint),
            placeholder is null ? null : new TextDefinition(KeyOf($"{field}.placeholder"), placeholder));
    }

    private TextKey KeyOf(string tail) => TextKey.Create($"{Prefix}{SerginStringKey.Separator}{tail}");
}
