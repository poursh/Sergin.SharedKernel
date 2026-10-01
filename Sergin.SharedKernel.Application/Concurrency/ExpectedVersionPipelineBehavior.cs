using MediatR;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Configuration;

namespace Sergin.SharedKernel.Application.Concurrency;

/// <summary>
/// Runs between the permission check and validation. Refuses a command whose configuration calls
/// <c>RequireExpectedVersion()</c> that arrives without an expected version, and turns the
/// <see cref="ConcurrencyConflictException"/> a save throws on a version mismatch into
/// <see cref="VersionErrors.Stale"/>. The check itself is the database's: the row-version interceptor puts the
/// expected version into the UPDATE's WHERE clause.
/// </summary>
internal sealed class ExpectedVersionPipelineBehavior<TRequest, TResponse>(
    ConcurrencyContext concurrency,
    CommandConfigurationRegistry configurations)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (concurrency.Expected is null
            && configurations.For(request.GetType()).RequiresExpectedVersion)
        {
            return ErrorOrResponse.TryFrom(VersionErrors.Required, out TResponse required)
                ? required
                : throw new InvalidOperationException(
                    $"{request.GetType().FullName} requires an expected version but does not answer ErrorOr, so the refusal cannot be returned.");
        }

        TResponse stale = default!;

        try
        {
            return await next(cancellationToken);
        }
        catch (ConcurrencyConflictException) when (ErrorOrResponse.TryFrom(VersionErrors.Stale, out stale))
        {
            return stale;
        }
    }
}
