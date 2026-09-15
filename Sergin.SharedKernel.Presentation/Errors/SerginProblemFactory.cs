using ErrorOr;
using Microsoft.AspNetCore.Http;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Presentation.Errors;

public static class SerginProblemFactory
{
    /// <summary>
    /// The one title every validation problem shares. Mirrors ErrorOr's own default validation code
    /// (<c>General.Validation</c>) in the <c>&lt;code&gt;.title</c> shape the other error types use, because a
    /// validation error's <see cref="Error.Code"/> is the offending property name, not a resource key.
    /// </summary>
    public const string ValidationTitleKey = "General.Validation.title";

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
            ErrorType.Validation => localizer[ValidationTitleKey],
            ErrorType.Unexpected => localizer[$"{error.Code}.title"],
            ErrorType.NotFound => localizer[$"{error.Code}.title"],
            ErrorType.Conflict => localizer[$"{error.Code}.title"],
            ErrorType.Forbidden => localizer[$"{error.Code}.title"],
            _ => "ServerFailure"
        };

    // A validation error's Description is the human message FluentValidation already produced — and
    // already localised, through its own LanguageManager from CultureInfo.CurrentUICulture — so it is
    // shown as is. Its Code is the property name, which is what the API groups a ValidationProblem by;
    // looking that up as a resource key would render "UserName" as the whole error.
    private static string GetDetail(Error error, ILocalizer localizer) =>
        error.Type switch
        {
            ErrorType.Validation => error.Description,
            ErrorType.Unexpected => localizer[error.Code],
            ErrorType.NotFound => localizer[error.Code],
            ErrorType.Conflict => localizer[error.Code],
            ErrorType.Forbidden => localizer[error.Code],
            _ => "An unexpected error occurred"
        };
}
