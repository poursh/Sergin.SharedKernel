using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sergin.SharedKernel.Application.Securities.Authorization;

namespace Sergin.SharedKernel.Presentation.WebApi.Authorization;

/// <summary>
/// Maps the <c>X-Sergin-Scope</c> request header onto <see cref="IAuthorizationScopeContext"/> for every endpoint in
/// a module group. No header leaves the request at the root scope. Anything but one non-empty GUID is a 400.
/// Endpoints need no change: the scope never appears on a request body.
/// </summary>
public sealed class AuthorizationScopeEndpointFilter : IEndpointFilter
{
    public const string HeaderName = "X-Sergin-Scope";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        HttpContext http = context.HttpContext;
        string sent = http.Request.Headers[HeaderName].ToString();

        if (!string.IsNullOrWhiteSpace(sent))
        {
            if (!Guid.TryParse(sent.Trim(), out Guid scopeId) || scopeId == Guid.Empty)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: $"Invalid {HeaderName} header",
                    detail: $"{HeaderName} must be one non-empty GUID: the id of the scope to work in.");
            }

            http.RequestServices.GetRequiredService<IAuthorizationScopeContext>().Set(scopeId);
        }

        return await next(context);
    }
}
