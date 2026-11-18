using FluentValidation.Results;

namespace Sergin.SharedKernel.Application.Commands;

/// <summary>
/// How a validator's failures become errors: one <see cref="ErrorType.Validation"/> error per failure, its code the
/// failure's property name and its description the failure's message. Every place that runs a request's validator
/// reports through this, so a refusal reads the same wherever it was found.
/// </summary>
internal static class ValidationFailureErrors
{
    public static List<Error> ToErrors(ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Errors.ConvertAll(error => Error.Validation(code: error.PropertyName, description: error.ErrorMessage));
    }
}
