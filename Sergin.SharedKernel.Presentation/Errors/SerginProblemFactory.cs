using ErrorOr;
using Microsoft.AspNetCore.Http;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Presentation.Errors;

public static class SerginProblemFactory
{
    public static SerginProblem Create(Error error, ILocalizer localizer)
        => new(GetStatusCode(error.Type), GetTitle(error, localizer), GetDetail(error, localizer), error.Type);

    public static int GetStatusCode(ErrorType errorType) =>
        errorType switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unexpected => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

    private static string GetTitle(Error error, ILocalizer localizer) =>
        error.Type switch
        {
            ErrorType.Validation => localizer[$"{error.Code}.title"],
            ErrorType.Unexpected => localizer[$"{error.Code}.title"],
            ErrorType.NotFound => localizer[$"{error.Code}.title"],
            ErrorType.Conflict => localizer[$"{error.Code}.title"],
            ErrorType.Forbidden => localizer[$"{error.Code}.title"],
            _ => "ServerFailure"
        };

    private static string GetDetail(Error error, ILocalizer localizer) =>
        error.Type switch
        {
            ErrorType.Validation => localizer[error.Code],
            ErrorType.Unexpected => localizer[error.Code],
            ErrorType.NotFound => localizer[error.Code],
            ErrorType.Conflict => localizer[error.Code],
            ErrorType.Forbidden => localizer[error.Code],
            _ => "An unexpected error occurred"
        };
}
