namespace Sergin.SharedKernel.Application.Commands.Queries;

/// <summary>
/// Every list feature declares its own request record, deriving from <see cref="ListQuery{TResponseData}"/>,
/// so the handler binds to that concrete type rather than to the open generic one. That is what makes
/// RequiredPermissionsAttribute applicable to a list slice and what lets a remote module register a
/// forwarding handler for it.
/// </summary>
public interface IListQueryHandler<TQuery, TResponseData> : IQueryHandler<TQuery, ListQueryResponse<TResponseData>>
    where TQuery : IListQuery<TResponseData>
    where TResponseData : notnull;
