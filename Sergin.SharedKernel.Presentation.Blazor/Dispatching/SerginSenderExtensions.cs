using Sergin.SharedKernel.Application.Dispatching;

namespace Sergin.SharedKernel.Presentation.Blazor.Dispatching;

public static class SerginSenderExtensions
{
    /// <summary>
    /// List queries have no dedicated command type — handlers implement IListQueryHandler&lt;TItem&gt;
    /// against the shared generic ListQuery&lt;TItem&gt;. This is the UI-side equivalent of
    /// ListQueryRequestModel.ToListQuery&lt;TItem&gt;(), without the [FromQuery] binding attributes.
    /// pageIndex is 1-based, matching PageIndex.Default; MudBlazor's TableState.Page is 0-based.
    /// </summary>
    public static Task<ErrorOr<ListQueryResponse<TItem>>> SendListAsync<TItem>(
        this ISerginSender sender, int pageSize, int pageIndex, CancellationToken cancellationToken = default)
        where TItem : notnull
        => sender.SendAsync(ListQueryFactory.Create<TItem>(pageSize, pageIndex), cancellationToken);
}
