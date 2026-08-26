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
    /// <summary>
    /// Projects the bound query-string values onto the shared paging value object. The request record
    /// itself is built by the endpoint, which is the only place that knows its feature's query type.
    /// </summary>
    public Paggination ToPaggination() => Paggination.Create(PageSize, PageIndex);
}
