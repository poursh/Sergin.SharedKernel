using System.Text.Json;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// What a <see cref="SerginAggregateList{TItem}"/> asks its page to load: the four parts of a <see cref="ListQuery"/>,
/// ready to pass to the feature's list record constructor.
/// </summary>
public sealed record SerginListRequest(Paggination Paggination, Term? Term, Filtering? Filtering, Sorting? Sorting);

/// <summary>
/// One filter applied to a list, as the list shows it — a chip reading <see cref="Label"/> — and sends it — a
/// <see cref="FilterData"/> with the field <see cref="Id"/>, <see cref="Mode"/> and <see cref="Value"/>.
/// </summary>
/// <param name="Id">The field id of the feature's <c>ListFieldSet</c>.</param>
/// <param name="Mode">How the value is compared.</param>
/// <param name="Value">A string, or for a range mode a two-element array of strings or nulls (an open end).</param>
/// <param name="Label">The chip text, e.g. <c>Manufacturer: Aster</c>.</param>
public sealed record SerginAppliedFilter(string Id, FilteringType Mode, object Value, string Label);

/// <summary>Builds the wire shapes a list query carries from what a list page holds.</summary>
public static class SerginListQueryParts
{
    private static readonly JsonSerializerOptions serializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static Filtering? ToFiltering(IReadOnlyCollection<SerginAppliedFilter> filters)
    {
        ArgumentNullException.ThrowIfNull(filters);

        return filters.Count == 0
            ? null
            : Filtering.Create(JsonSerializer.Serialize(
                filters.Select(filter => new { filter.Id, filter.Value, Mode = filter.Mode.ToString() }), serializerOptions));
    }

    public static Sorting? ToSorting(string? sortLabel, bool descending) =>
        string.IsNullOrEmpty(sortLabel)
            ? null
            : Sorting.Create(JsonSerializer.Serialize(new[] { new SortingData(sortLabel, descending) }, serializerOptions));
}
