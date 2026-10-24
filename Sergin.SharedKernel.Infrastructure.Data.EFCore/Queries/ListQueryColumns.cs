using Sergin.SharedKernel.Application.Commands.Queries;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Queries;

/// <summary>
/// A query repository's half of a list feature's <see cref="ListFieldSet"/>: the SQL expression each field id
/// reads, the columns a <see cref="Term"/> searches, and the tie-breaker every page is finally ordered by so paging
/// is stable. Built once per repository, as a static field.
/// </summary>
public sealed class ListQueryColumns
{
    public ListQueryColumns(
        ListFieldSet fields,
        IReadOnlyDictionary<string, string> columns,
        IReadOnlyCollection<string> searchColumns,
        string tieBreaker)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(searchColumns);
        ArgumentException.ThrowIfNullOrWhiteSpace(tieBreaker);

        IReadOnlyCollection<string> unmapped = [.. fields.Fields.Select(field => field.Id).Where(id => !columns.ContainsKey(id))];

        if (unmapped.Count > 0)
        {
            throw new ArgumentException($"List field(s) {string.Join(", ", unmapped)} have no column.", nameof(columns));
        }

        Fields = fields;
        Columns = columns;
        SearchColumns = searchColumns;
        TieBreaker = tieBreaker;
    }

    public ListFieldSet Fields { get; }

    public IReadOnlyDictionary<string, string> Columns { get; }

    public IReadOnlyCollection<string> SearchColumns { get; }

    public string TieBreaker { get; }
}
