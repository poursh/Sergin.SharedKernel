using System.Globalization;
using System.Text.Json;

namespace Sergin.SharedKernel.Application.Commands.Queries;

/// <summary>The type a list field's filter values are read as.</summary>
public enum ListFieldKind
{
    Text,
    Identifier,
    Instant,
}

/// <summary>
/// One field of a list query that a caller may sort or filter on. The id is the wire name a
/// <see cref="SortingData"/> or <see cref="FilterData"/> carries; only an id declared here ever reaches SQL.
/// </summary>
/// <param name="Id">The wire name, camelCase, matching the list item's property where there is one.</param>
/// <param name="Kind">How a filter value is parsed.</param>
/// <param name="Sortable">Whether a <see cref="SortingData"/> may name it.</param>
/// <param name="FilterModes">The <see cref="FilteringType"/>s a <see cref="FilterData"/> may use on it; empty for none.</param>
public sealed record ListField(string Id, ListFieldKind Kind, bool Sortable, IReadOnlySet<FilteringType> FilterModes)
{
    public static ListField SortOnly(string id, ListFieldKind kind = ListFieldKind.Text) =>
        new(id, kind, Sortable: true, new HashSet<FilteringType>());

    public static ListField FilterOnly(string id, ListFieldKind kind, params FilteringType[] modes) =>
        new(id, kind, Sortable: false, new HashSet<FilteringType>(modes));
}

/// <summary>
/// The fields a list query declares, shared by its validator (which refuses anything else) and its query
/// repository (which maps each id to a column). A feature declares one in its <c>.Application.Contracts</c>.
/// </summary>
public sealed class ListFieldSet
{
    /// <summary>The modes <c>ListQuerySql</c> knows how to translate; a field may declare only these.</summary>
    public static readonly IReadOnlySet<FilteringType> SupportedModes = new HashSet<FilteringType>
    {
        FilteringType.equals,
        FilteringType.contains,
        FilteringType.startsWith,
        FilteringType.between,
        FilteringType.betweenInclusive,
        FilteringType.greaterThanOrEqualTo,
        FilteringType.lessThanOrEqualTo,
    };

    private readonly Dictionary<string, ListField> fields;

    private ListFieldSet(Dictionary<string, ListField> fields) => this.fields = fields;

    public IReadOnlyCollection<ListField> Fields => fields.Values;

    public static ListFieldSet Create(params ListField[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        Dictionary<string, ListField> byId = new(StringComparer.Ordinal);

        foreach (ListField field in fields)
        {
            IReadOnlyCollection<FilteringType> unsupported = [.. field.FilterModes.Where(mode => !SupportedModes.Contains(mode))];

            if (unsupported.Count > 0)
            {
                throw new ArgumentException(
                    $"List field '{field.Id}' declares unsupported filter mode(s) {string.Join(", ", unsupported)}.", nameof(fields));
            }

            if (!byId.TryAdd(field.Id, field))
            {
                throw new ArgumentException($"List field '{field.Id}' is declared twice.", nameof(fields));
            }
        }

        return new ListFieldSet(byId);
    }

    public ListField? Find(string id) => fields.GetValueOrDefault(id);
}

/// <summary>
/// Reads a <see cref="FilterData"/>'s value as its field's kind. The value arrives as a
/// <see cref="JsonElement"/> (a <see cref="Filtering"/> is always deserialized from JSON); a range mode
/// expects a two-element array, every other mode a single value.
/// </summary>
public static class ListFilterValues
{
    public static bool IsRange(FilteringType mode) =>
        mode is FilteringType.between or FilteringType.betweenInclusive;

    /// <summary>
    /// Parses <paramref name="filter"/>'s value; false when it is missing, the wrong shape or not of the field's
    /// kind. A range may leave one end null (an open end), but not both.
    /// </summary>
    public static bool TryParse(FilterData filter, ListField field, out IReadOnlyList<object?> values)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(field);

        values = [];

        if (!Enum.TryParse(filter.Mode, out FilteringType mode))
        {
            return false;
        }

        if (IsRange(mode))
        {
            if (filter.Value is not JsonElement { ValueKind: JsonValueKind.Array } array || array.GetArrayLength() != 2)
            {
                return false;
            }

            List<object?> ends = new(2);

            foreach (JsonElement end in array.EnumerateArray())
            {
                if (end.ValueKind == JsonValueKind.Null)
                {
                    ends.Add(null);
                }
                else if (TryRead(end, field.Kind, out object? value))
                {
                    ends.Add(value);
                }
                else
                {
                    return false;
                }
            }

            values = ends;
            return ends.Exists(end => end is not null);
        }

        if (filter.Value is JsonElement element && TryRead(element, field.Kind, out object? single))
        {
            values = [single];
            return true;
        }

        return false;
    }

    private static bool TryRead(JsonElement element, ListFieldKind kind, out object? value)
    {
        value = null;

        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string text = element.GetString()!;

        switch (kind)
        {
            case ListFieldKind.Text:
                value = text;
                return text.Length > 0;
            case ListFieldKind.Identifier:
                bool isGuid = Guid.TryParse(text, out Guid guid);
                value = guid;
                return isGuid;
            case ListFieldKind.Instant:
                bool isDate = DateTime.TryParse(
                    text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime date);
                value = date;
                return isDate;
            default:
                return false;
        }
    }
}
