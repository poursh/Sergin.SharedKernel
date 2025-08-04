using Microsoft.AspNetCore.Mvc;
using Sergin.SharedKernel.Application.Commands.Queries;

namespace Sergin.SharedKernel.Presentation.WebApi.Endpoints;

public sealed record ListQueryRequestModel(
    [FromQuery] int PageSize = 10,
    [FromQuery] int PageIndex = 1,
    [FromQuery] string? Term = default,
    [FromQuery] string? Filtering = default,
    [FromQuery] string? Sorting = default)
{
    public ListQuery<TResponseData> ToListQuery<TResponseData>()
        where TResponseData : notnull
    {
        return ListQueryFactory.Create<TResponseData>(
            PageSize, PageIndex, Term);
    }
}
