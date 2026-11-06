using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Sergin.SharedKernel.Application.Localizations.Validation;

namespace Sergin.SharedKernel.Application.Commands;

/// <summary>
/// Runs the request's validator, if any, inside <see cref="ValidationTexts.Use"/>, so its messages and field names
/// come from this host's text catalog in the caller's culture.
/// </summary>
internal sealed class ValidationPipelineBehavior<TRequest, TResponse>(
    ValidationTexts? texts = null, IValidator<TRequest>? validator = null)
    : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : IErrorOr
{
    private readonly IValidator<TRequest>? _validator = validator;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_validator is null)
        {
            return await next(cancellationToken);
        }

        ValidationResult validationResult;

        using (texts?.Use())
        {
            validationResult = await _validator.ValidateAsync(request, cancellationToken);
        }

        if (validationResult.IsValid)
        {
            return await next(cancellationToken);
        }

        List<Error> errors = validationResult.Errors
            .ConvertAll(error => Error.Validation(
                code: error.PropertyName,
                description: error.ErrorMessage));

        return (dynamic)errors;
    }
}
