using ErrorOr;
using Microsoft.AspNetCore.Http;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Presentation.WebApi.Endpoints.Results;

public static class ApiProblemResults
{
    public static IResult Problem(Error error, ILocalizer l)
    {
        return Microsoft.AspNetCore.Http.Results.Problem(
            title: GetTitle(error),
            detail: GetDetail(error),
            statusCode: GetStatusCode(error.Type));

        string GetTitle(Error error) =>
            error.Type switch
            {
                ErrorType.Validation => l[$"{error.Code}.title"],
                ErrorType.Unexpected => l[$"{error.Code}.title"],
                ErrorType.NotFound => l[$"{error.Code}.title"],
                ErrorType.Conflict => l[$"{error.Code}.title"],
                ErrorType.Forbidden => l[$"{error.Code}.title"],
                _ => "ServerFailure"
            };

        string GetDetail(Error error) =>
            error.Type switch
            {
                ErrorType.Validation => l[error.Code],
                ErrorType.Unexpected => l[error.Code],
                ErrorType.NotFound => l[error.Code],
                ErrorType.Conflict => l[error.Code],
                ErrorType.Forbidden => l[error.Code],
                _ => "An unexpected error occurred"
            };

        static int GetStatusCode(ErrorType errorType) =>
            errorType switch
            {
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.Unexpected => StatusCodes.Status400BadRequest,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status500InternalServerError
            };

       
    }
}
