using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Presentation.WebApi.Concurrency;

/// <summary>
/// Maps HTTP's conditional-request headers onto <see cref="ConcurrencyContext"/> for every endpoint in a module
/// group: a strong <c>If-Match: "&lt;version&gt;"</c> becomes the expected version, and a version the send
/// read or wrote comes back as <c>ETag</c>. <c>*</c> and weak tags carry no version this platform can check,
/// so they count as absent, and a command marked [RequiresExpectedVersion] then answers 428. Anything else in
/// If-Match is a 400. Endpoints need no change: the version never appears on a request or response body.
/// </summary>
public sealed class ExpectedVersionEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        HttpContext http = context.HttpContext;
        ConcurrencyContext concurrency = http.RequestServices.GetRequiredService<ConcurrencyContext>();
        string ifMatch = http.Request.Headers.IfMatch.ToString();

        if (!string.IsNullOrWhiteSpace(ifMatch))
        {
            if (!TryParse(ifMatch.Trim(), out RowVersion? expected))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid If-Match header",
                    detail: "If-Match must be one strong entity tag: the quoted version an ETag returned.");
            }

            concurrency.Expected = expected;
        }

        object? result = await next(context);

        // The result has not been written yet, so the header still reaches the response.
        if (concurrency.Current is { } current)
        {
            http.Response.Headers.ETag = $"\"{current.Value}\"";
        }

        return result;
    }

    /// <summary>True for a usable header; <paramref name="version"/> is null for * and weak tags.</summary>
    private static bool TryParse(string value, out RowVersion? version)
    {
        version = null;

        if (value == "*" || value.StartsWith("W/", StringComparison.Ordinal))
        {
            return true;
        }

        if (value.Length > 2
            && value[0] == '"'
            && value[^1] == '"'
            && Guid.TryParse(value.AsSpan(1, value.Length - 2), out Guid guid)
            && guid != Guid.Empty)
        {
            version = RowVersion.Create(guid);
            return true;
        }

        return false;
    }
}
