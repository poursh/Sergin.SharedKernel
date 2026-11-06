using Sergin.SharedKernel.Domain.Localizations;

namespace Sergin.SharedKernel.Application.Localizations;

/// <summary>
/// A named group of declared texts under one module, e.g. <c>device-management.devices</c>. Segment names may be
/// written PascalCase; keys always come out kebab case.
/// </summary>
public sealed class TextCategory
{
    private const string FieldsSegment = "fields";

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
