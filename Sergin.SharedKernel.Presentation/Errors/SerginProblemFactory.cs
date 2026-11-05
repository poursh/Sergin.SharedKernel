using System.Globalization;
using Microsoft.Extensions.Localization;
using ErrorOr;
using Microsoft.AspNetCore.Http;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Application.Localizations.Common;
using Sergin.SharedKernel.Domain.Localizations;

namespace Sergin.SharedKernel.Presentation.Errors;

public static class SerginProblemFactory
{
    /// <summary>
    /// The key of the one title every validation problem shares (<see cref="ErrorTexts.ValidationTitle"/>), because a
    /// validation error's <see cref="Error.Code"/> is the offending property name, not a text key.
    /// </summary>
    public const string ValidationTitleKey = "common.errors.validation.title";

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

    // Every code that reaches a person has a declared title and detail (ErrorTexts, common.errors.<code>.*); a code
    // none is declared for shows its type's pair rather than the raw code.
    private static string GetTitle(Error error, IStringLocalizer localizer) =>
        error.Type == ErrorType.Validation
            ? localizer.Text(ErrorTexts.ValidationTitle)
            : localizer.Text(ErrorTexts.For(error.Code, error.Type).Title);

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
    // looking that up as a text key would render "UserName" as the whole error.
    private static string GetDetail(Error error, IStringLocalizer localizer) =>
        error.Type == ErrorType.Validation
            ? error.Description
            : localizer.Text(ErrorTexts.For(error.Code, error.Type).Detail);
}
