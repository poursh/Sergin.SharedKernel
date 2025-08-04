using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Presentation.WebApi.Endpoints.Results;

public class EndpointResult<TValue>(ErrorOr<TValue> result) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ILocalizer localizer = httpContext.RequestServices.GetRequiredService<ILocalizer>();
        return result.ToApiResult(localizer).ExecuteAsync(httpContext);
    }
}
