using System.Text;
using Dapper;
using Sergin.SharedKernel.Application.Commands.Queries;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Queries;

/// <summary>The two fragments <see cref="ListQuerySql.Apply"/> builds.</summary>
/// <param name="Where">Empty, or <c> AND …</c> to append to the query's own WHERE clause — in the count and the page alike.</param>
/// <param name="OrderBy">A complete <c>ORDER BY</c> clause: the requested sorts, then the tie-breaker.</param>
public sealed record ListQuerySqlParts(string Where, string OrderBy);

/// <summary>
/// Turns a <see cref="ListQuery"/>'s <see cref="Term"/>, <see cref="Filtering"/> and <see cref="Sorting"/> into SQL
/// for a raw-SQL list read. Only expressions from <see cref="ListQueryColumns"/> reach the SQL text; every value is
/// a parameter (<c>@Lq0</c>, <c>@Lq1</c>, … and <c>@LqTerm</c>).
/// </summary>
/// <remarks>
/// The feature's <c>ListQueryValidator</c> has already refused an unknown field, mode or value, so the
/// <see cref="ArgumentException"/>s here are a backstop for a caller that skipped the pipeline.
/// </remarks>
public static class ListQuerySql
{
    public const string TermParameter = "LqTerm";

    public static ListQuerySqlParts Apply(ListQuery query, ListQueryColumns columns, DynamicParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(parameters);

        List<string> predicates = [];

        if (query.Term?.Value.Trim() is { Length: > 0 } term && columns.SearchColumns.Count > 0)
        {
            parameters.Add(TermParameter, $"%{EscapeLike(term)}%");
            predicates.Add("(" + string.Join(" OR ", columns.SearchColumns.Select(column => $"{column} ILIKE @{TermParameter}")) + ")");
        }

        int index = 0;

        foreach (FilterData filter in query.Filtering?.Filters ?? [])
        {
            predicates.Add(Predicate(filter, columns, parameters, ref index));
        }

        string where = predicates.Count == 0 ? string.Empty : " AND " + string.Join(" AND ", predicates);

        return new ListQuerySqlParts(where, OrderBy(query.Sorting, columns));
    }

    /// <summary>Escapes <c>\</c>, <c>%</c> and <c>_</c>, so a search for them finds them; Postgres' default LIKE escape is <c>\</c>.</summary>
    public static string EscapeLike(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        StringBuilder escaped = new(value.Length);

        foreach (char character in value)
        {
            if (character is '\\' or '%' or '_')
            {
                escaped.Append('\\');
            }

            escaped.Append(character);
        }

        return escaped.ToString();
    }

    private static string Predicate(FilterData filter, ListQueryColumns columns, DynamicParameters parameters, ref int index)
    {
        if (columns.Fields.Find(filter.Id) is not { } field
            || !Enum.TryParse(filter.Mode, out FilteringType mode)
            || !field.FilterModes.Contains(mode))
        {
            throw new ArgumentException($"'{filter.Id}' cannot be filtered with '{filter.Mode}'.", nameof(filter));
        }

        if (!ListFilterValues.TryParse(filter, field, out IReadOnlyList<object?> values))
        {
            throw new ArgumentException($"'{filter.Id}' has an invalid value.", nameof(filter));
        }

        string column = columns.Columns[field.Id];
        int next = index;

        string Bind(object? value)
        {
            string name = $"Lq{next++}";
            parameters.Add(name, value);
            return "@" + name;
        }

        string predicate = mode switch
        {
            FilteringType.equals => $"{column} = {Bind(values[0])}",
            FilteringType.contains => $"{column} ILIKE {Bind($"%{EscapeLike((string)values[0]!)}%")}",
            FilteringType.startsWith => $"{column} ILIKE {Bind($"{EscapeLike((string)values[0]!)}%")}",
            FilteringType.greaterThanOrEqualTo => $"{column} >= {Bind(values[0])}",
            FilteringType.lessThanOrEqualTo => $"{column} <= {Bind(values[0])}",
            FilteringType.between or FilteringType.betweenInclusive => Range(column, values, mode == FilteringType.betweenInclusive, Bind),
            _ => throw new ArgumentException($"'{mode}' is not supported.", nameof(filter)),
        };

        index = next;
        return predicate;
    }

    /// <summary><c>between</c> is half-open (<c>from ≤ x &lt; to</c>), <c>betweenInclusive</c> closed; a null end is open.</summary>
    private static string Range(string column, IReadOnlyList<object?> values, bool inclusive, Func<object?, string> bind)
    {
        List<string> ends = [];

        if (values[0] is { } from)
        {
            ends.Add($"{column} >= {bind(from)}");
        }

        if (values[1] is { } to)
        {
            ends.Add($"{column} {(inclusive ? "<=" : "<")} {bind(to)}");
        }

        return "(" + string.Join(" AND ", ends) + ")";
    }

    private static string OrderBy(Sorting? sorting, ListQueryColumns columns)
    {
        List<string> terms = [];

        foreach (SortingData sort in sorting?.Sorts ?? [])
        {
            if (columns.Fields.Find(sort.Id) is not { Sortable: true } field)
            {
                throw new ArgumentException($"'{sort.Id}' is not a sortable field.", nameof(sorting));
            }

            terms.Add($"{columns.Columns[field.Id]} {(sort.Desc ? "DESC" : "ASC")}");
        }

        terms.Add(columns.TieBreaker);

        return "ORDER BY " + string.Join(", ", terms);
    }
}
