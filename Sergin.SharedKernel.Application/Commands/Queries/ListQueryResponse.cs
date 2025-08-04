namespace RTS.Common.Domain.Repository.Query;

public sealed record ListQueryResponse<TData>
{
    public ListQueryResponse(IEnumerable<TData> data, int total)
    {
        Data = [.. data];
        Total = total;
    }

    public IReadOnlyCollection<TData> Data { get; private set; }
    public int Total { get; private set; }
}
