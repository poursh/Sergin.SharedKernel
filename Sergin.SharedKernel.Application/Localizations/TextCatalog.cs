using System.Diagnostics.CodeAnalysis;

namespace Sergin.SharedKernel.Application.Localizations;

/// <summary>
/// Every text the platform declares, collected from each module's <see cref="TextSource"/> plus SharedKernel's own.
/// Built once at host start, so a declaration mistake stops start: one key declared twice (or with two defaults),
/// or a key outside its source's module segment.
/// </summary>
public sealed class TextCatalog
{
    private readonly IReadOnlyList<TextCatalogEntry> entries;
    private readonly IReadOnlyList<FieldText> fields;
    private readonly Dictionary<string, TextDefinition> byKey;

    private TextCatalog(IReadOnlyList<TextCatalogEntry> entries, IReadOnlyList<FieldText> fields)
    {
        this.entries = entries;
        this.fields = fields;
        byKey = entries.ToDictionary(entry => entry.Definition.Key.Value, entry => entry.Definition, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<TextCatalogEntry> Entries => entries;

    public static TextCatalog FromSources(IEnumerable<TextSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        List<TextCatalogEntry> collected = [];
        List<FieldText> collectedFields = [];

        foreach (TextSource source in sources)
        {
            foreach (TextCatalogEntry entry in source.Entries)
            {
                if (entry.Module != source.ModuleSegment)
                {
                    throw new InvalidOperationException(
                        $"{entry.DeclaringType.FullName} declares '{entry.Definition.Key.Value}', which is outside its module: "
                        + $"expected '{source.ModuleSegment}.*'.");
                }

                collected.Add(entry);
            }

            collectedFields.AddRange(source.Fields);
        }

        CheckKeys(collected);

        return new TextCatalog(
            [.. collected.DistinctBy(entry => entry.Definition.Key.Value, StringComparer.Ordinal)],
            [.. collectedFields.DistinctBy(field => (field.Category.Prefix, field.PropertyName))]);
    }

    public bool TryGet(string key, [NotNullWhen(true)] out TextDefinition? definition)
    {
        ArgumentNullException.ThrowIfNull(key);

        return byKey.TryGetValue(key, out definition);
    }

    public IReadOnlyList<FieldText> FieldsOf(TextCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);

        return [.. fields.Where(field => field.Category.Prefix == category.Prefix)];
    }

    public FieldText? FindField(TextCategory category, string propertyName) =>
        FieldsOf(category).FirstOrDefault(field => string.Equals(field.PropertyName, propertyName, StringComparison.Ordinal));

    private static void CheckKeys(List<TextCatalogEntry> collected)
    {
        foreach (IGrouping<string, TextCatalogEntry> group in collected.GroupBy(entry => entry.Definition.Key.Value, StringComparer.Ordinal))
        {
            TextCatalogEntry[] same = [.. group.DistinctBy(entry => (entry.DeclaringType, entry.Definition.Default))];

            if (same.Length < 2)
            {
                continue;
            }

            string types = string.Join(", ", same.Select(entry => entry.DeclaringType.FullName).Distinct(StringComparer.Ordinal));
            string[] defaults = [.. same.Select(entry => $"'{entry.Definition.Default}'").Distinct(StringComparer.Ordinal)];

            throw new InvalidOperationException(defaults.Length > 1
                ? $"'{group.Key}' is declared with more than one default: {string.Join(", ", defaults)} (in {types}). A key has one default."
                : $"'{group.Key}' is declared more than once, in {types}. A key belongs to exactly one declaration.");
        }
    }
}
