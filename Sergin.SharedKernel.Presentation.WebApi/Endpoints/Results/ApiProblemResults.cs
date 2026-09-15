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

    /// <summary>
    /// Maps a handler's whole error list. Validation is the one error type that routinely yields several
    /// at once, so when every error is a validation error the response is the standard
    /// <see cref="HttpValidationProblemDetails"/> shape — one entry per property, every message under it —
    /// rather than the first message alone. Any other mix falls back to the first error, as before.
    /// </summary>
    public static IResult Problem(IReadOnlyList<Error> errors, ILocalizer l)
    {
        if (errors.Any(error => error.Type != ErrorType.Validation))
        {
            return Problem(errors[0], l);
        }

        var errorsByProperty = errors
            .GroupBy(error => error.Code, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray(),
                StringComparer.Ordinal);

        return Microsoft.AspNetCore.Http.Results.ValidationProblem(
            errorsByProperty,
            title: l[SerginProblemFactory.ValidationTitleKey]);
    }
}
