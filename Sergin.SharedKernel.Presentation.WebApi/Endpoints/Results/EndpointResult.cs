using Microsoft.Extensions.Localization;
﻿using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Sergin.SharedKernel.Presentation.WebApi.Endpoints.Results;

public class EndpointResult<TValue>(ErrorOr<TValue> result) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        IStringLocalizer localizer = httpContext.RequestServices.GetRequiredService<IStringLocalizer>();
        return result.ToApiResult(localizer).ExecuteAsync(httpContext);
    }
}
