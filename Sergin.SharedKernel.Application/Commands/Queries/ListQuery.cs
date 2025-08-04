using System.Text.Json;
using Ardalis.GuardClauses;

namespace Sergin.SharedKernel.Application.Commands.Queries;

public sealed record ListQuery<TResponseData>
    : ListQuery, IListQuery<TResponseData>
    where TResponseData : notnull
{
    public ListQuery(Paggination paggination, Term? globalTerm = default, Filtering? filtering = default, Sorting? sorting = default) : base(paggination, globalTerm, filtering, sorting)
    {
    }
}

public sealed record ListQuery<TRequestData, TResponseData>
    : ListQuery, IListQuery<TResponseData>
    where TRequestData : notnull
    where TResponseData : notnull
{
    public ListQuery(TRequestData requestData, Paggination paggination, Term? globalTerm = default, Filtering? filtering = default, Sorting? sorting = default) : base(paggination, globalTerm, filtering, sorting)
    {
        RequestData = requestData;
    }

    public TRequestData RequestData { get; private set; }
}

public record ListQuery(Paggination Paggination, Term? Term = default, Filtering? Filtering = default, Sorting? Sorting = default);

public sealed record Paggination
{
    public static readonly Paggination Default = Create(PageSize.Default, PageIndex.Default);
    public Paggination()
    {
        Size = PageSize.Default;
        Index = PageIndex.Default;
    }

    public Paggination(PageSize size)
    {
        Size = size;
        Index = PageIndex.Default;
    }

    public Paggination(PageSize size, PageIndex index)
    {
        Size = size;
        Index = index;
    }

    public PageSize Size { get; private set; }
    public PageIndex Index { get; private set; }

    public int Skip => Size * (Index - 1);

    public static Paggination Create(PageSize size, PageIndex index)
    {
        return new Paggination(size, index);
    }
}
public sealed record PageSize
{
    public const int MaxSize = int.MaxValue;

    public static readonly PageSize Default = 10;

    public PageSize(int size)
    {
        Guard.Against.NegativeOrZero(size);
        Guard.Against.InvalidInput(size, nameof(size), s => s <= MaxSize);

        Value = size;
    }

    public int Value { get; private set; }

    public static implicit operator int(PageSize? pageSize) => pageSize?.Value ?? default;
    public static implicit operator int?(PageSize? pageSize) => pageSize?.Value;
    public static implicit operator PageSize?(int? pageSize) => pageSize is not null ? new(pageSize.Value) : Default;
    public static implicit operator PageSize(int pageSize) => new(pageSize);
}
public sealed record PageIndex
{
    public static readonly PageIndex Default = 1;

    public PageIndex(int index)
    {
        Guard.Against.NegativeOrZero(index);

        Value = index;
    }

    public int Value { get; private set; }

    public static implicit operator int(PageIndex? pageIndex) => pageIndex?.Value ?? default;
    public static implicit operator int?(PageIndex? pageIndex) => pageIndex?.Value;
    public static implicit operator PageIndex?(int? pageIndex) => pageIndex is not null ? new(pageIndex.Value) : Default;
    public static implicit operator PageIndex(int pageIndex) => new(pageIndex);
}
public sealed record Term
{
    public Term(string filter)
    {
        Guard.Against.Null(filter);

        Value = filter;
    }

    public string Value { get; private set; }

    public static implicit operator string(Term? term) => term?.Value;
    public static implicit operator Term(string? term) => term is not null ? new(term) : null;
}
public sealed record Filtering
{
    private static readonly JsonSerializerOptions _serializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private Filtering(string filter)
    {
        Value = filter;
        Filters = JsonSerializer.Deserialize<FilterData[]>(filter, _serializerOptions)!;
    }

    public string Value { get; private set; }
    public IEnumerable<FilterData> Filters { get; private set; }

    public static Filtering Create(string filter)
    {
        Guard.Against.NullOrEmpty(filter);

        return new Filtering(filter);
    }

    public static implicit operator Filtering(string? filter) => filter is not null ? Create(filter) : null;
}
public sealed record Sorting
{
    private static readonly JsonSerializerOptions _serializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public Sorting(string value)
    {
        Value = value;
        Sorts = JsonSerializer.Deserialize<IEnumerable<SortingData>>(value, _serializerOptions)!;
    }

    public string Value { get; private set; }
    public IEnumerable<SortingData> Sorts { get; private set; }

    public static Sorting Create(string sort)
    {
        Guard.Against.NullOrEmpty(sort);

        return new Sorting(sort);
    }

    public static implicit operator string(Sorting? sort) => sort?.Value;
    public static implicit operator Sorting(string? sort) => sort is not null ? new(sort) : null;
}

public sealed record SortingData(string Id, bool Desc);
public sealed class FilterData
{
    public string Id { get; set; }
    public object Value { get; set; }
    public string Mode { get; set; }
    public FilteringType FilterType => Enum.Parse<FilteringType>(Mode);
}

public enum FilteringType
{
    between,
    betweenInclusive,
    contains,
    empty,
    endsWith,
    equals,
    greaterThan,
    greaterThanOrEqualTo,
    lessThan,
    lessThanOrEqualTo,
    notEmpty,
    notEquals,
    startsWith,
    equalsString,
    none
}

public static class ListQueryFactory
{
    public static ListQuery<TResponseData> Create<TResponseData>(Paggination? paggination, Term? Term = default, Filtering? Filtering = default, Sorting? Sorting = default)
        where TResponseData : notnull
    {
        return new ListQuery<TResponseData>(paggination ?? Paggination.Default, Term, Filtering, Sorting);
    }

    public static ListQuery<TResponseData> Create<TResponseData>(PageSize size, PageIndex index, Term? Term = default, Filtering? Filtering = default, Sorting? Sorting = default)
        where TResponseData : notnull
    {
        return new ListQuery<TResponseData>(Paggination.Create(size, index), Term, Filtering, Sorting);
    }

    public static ListQuery<TRequestData, TResponseData> Create<TRequestData, TResponseData>(
        TRequestData requestData, PageSize size, PageIndex index, Term? Term = default, Filtering? Filtering = default, Sorting? Sorting = default)
        where TRequestData : notnull
        where TResponseData : notnull
    {
        return new ListQuery<TRequestData, TResponseData>(requestData, Paggination.Create(size, index), Term, Filtering, Sorting);
    }

    public static ListQuery<TRequestData, TResponseData> Create<TRequestData, TResponseData>(
        TRequestData requestData, Paggination? paggination, Term? Term = default, Filtering? Filtering = default, Sorting? Sorting = default)
        where TRequestData : notnull
        where TResponseData : notnull
    {
        return new ListQuery<TRequestData, TResponseData>(requestData, paggination ?? Paggination.Default, Term, Filtering, Sorting);
    }
}
