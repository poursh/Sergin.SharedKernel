using ErrorOr;
using Microsoft.AspNetCore.Http;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Presentation.Errors;

namespace Sergin.SharedKernel.Presentation.WebApi.Endpoints.Results;

public static class ApiProblemResults
{
    public static IResult Problem(Error error, ILocalizer l)
    {
        SerginProblem problem = SerginProblemFactory.Create(error, l);

        return Microsoft.AspNetCore.Http.Results.Problem(
            title: problem.Title,
            detail: problem.Detail,
            statusCode: problem.StatusCode);
    }
}
