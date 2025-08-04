using ErrorOr;
using Microsoft.AspNetCore.Http;
using Sergin.SharedKernel.Application.Localizations;

using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Sergin.SharedKernel.Presentation.WebApi.Endpoints.Results;
public static class ResultExtensions
{
    public static IResult ToApiResult<TIn>(
        this ErrorOr<TIn> result)
    {
        return new EndpointResult<TIn>(result);
    }

    internal static IResult ToApiResult<TIn>(
        this ErrorOr<TIn> result, ILocalizer localizer)
    {
        return result.Match(HttpResults.Ok, (r) => ApiProblemResults.Problem(r[0], localizer));
    }
}
