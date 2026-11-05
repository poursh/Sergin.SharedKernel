using System.Globalization;
using Microsoft.Extensions.Localization;
using ErrorOr;
using Microsoft.AspNetCore.Http;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Domain.Localizations;

namespace Sergin.SharedKernel.Presentation.Errors;

public static class SerginProblemFactory
{
    /// <summary>
    /// The one title every validation problem shares. Mirrors ErrorOr's own default validation code
    /// (<c>General.Validation</c>) in the <c>&lt;code&gt;.title</c> shape the other error types use, because a
    /// validation error's <see cref="Error.Code"/> is the offending property name, not a resource key.
    /// </summary>
    public const string ValidationTitleKey = "General.Validation.title";

    private static readonly IReadOnlyDictionary<string, object?> noArguments = new Dictionary<string, object?>(StringComparer.Ordinal);

    public static SerginProblem Create(Error error, IStringLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        return new(
            GetStatusCode(error.Type),
            GetTitle(error, localizer),
            DeclaredDetail(error, localizer) ?? GetDetail(error, localizer),
            error.Type);
    }

    public static int GetStatusCode(ErrorType errorType) =>
        errorType switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unexpected => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            (ErrorType)VersionErrors.RequiredType => StatusCodes.Status428PreconditionRequired,
            (ErrorType)VersionErrors.StaleType => StatusCodes.Status412PreconditionFailed,
            _ => StatusCodes.Status500InternalServerError
        };

    private static string GetTitle(Error error, IStringLocalizer localizer) =>
        error.Type switch
        {
            ErrorType.Validation => localizer[ValidationTitleKey],
            ErrorType.Unexpected => localizer[$"{error.Code}.title"],
            ErrorType.NotFound => localizer[$"{error.Code}.title"],
            ErrorType.Conflict => localizer[$"{error.Code}.title"],
            ErrorType.Forbidden => localizer[$"{error.Code}.title"],
            (ErrorType)VersionErrors.RequiredType => localizer[$"{error.Code}.title"],
            (ErrorType)VersionErrors.StaleType => localizer[$"{error.Code}.title"],
            _ => "ServerFailure"
        };

    // An error that names a declared text in its metadata (ErrorText.MetadataKey) shows that text, its named
    // placeholders filled from ErrorText.ArgumentsKey, whatever its type: the code stays what it is (a validation
    // error's is the property name a form and the API group by). A key the catalog does not know falls through to the
    // rule below rather than showing the raw key.
    private static string? DeclaredDetail(Error error, IStringLocalizer localizer)
    {
        if (error.Metadata is not { } metadata
            || !metadata.TryGetValue(ErrorText.MetadataKey, out object? key)
            || key is not string name)
        {
            return null;
        }

        LocalizedString found = localizer[name];

        if (found.ResourceNotFound)
        {
            return null;
        }

        IReadOnlyDictionary<string, object?> arguments =
            metadata.TryGetValue(ErrorText.ArgumentsKey, out object? values) && values is not null
                ? TextPlaceholders.ArgumentsOf(values)
                : noArguments;

        return TextPlaceholders.Format(found.Value, arguments, CultureInfo.CurrentCulture);
    }

    // A validation error's Description is the human message FluentValidation already produced — and
    // already localised, through its own LanguageManager from CultureInfo.CurrentUICulture — so it is
    // shown as is. Its Code is the property name, which is what the API groups a ValidationProblem by;
    // looking that up as a resource key would render "UserName" as the whole error.
    private static string GetDetail(Error error, IStringLocalizer localizer) =>
        error.Type switch
        {
            ErrorType.Validation => error.Description,
            ErrorType.Unexpected => localizer[error.Code],
            ErrorType.NotFound => localizer[error.Code],
            ErrorType.Conflict => localizer[error.Code],
            ErrorType.Forbidden => localizer[error.Code],
            (ErrorType)VersionErrors.RequiredType => localizer[error.Code],
            (ErrorType)VersionErrors.StaleType => localizer[error.Code],
            _ => "An unexpected error occurred"
        };
}
