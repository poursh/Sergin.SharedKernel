namespace Sergin.SharedKernel.Application.Commands.Queries;

public interface IListQueryHandler<TResponseData> : IQueryHandler<ListQuery<TResponseData>, ListQueryResponse<TResponseData>>
    where TResponseData : notnull;
public interface IListQueryHandler<TRequestData, TResponseData> : IQueryHandler<ListQuery<TRequestData, TResponseData>, ListQueryResponse<TResponseData>>
    where TRequestData : notnull
    where TResponseData : notnull;
