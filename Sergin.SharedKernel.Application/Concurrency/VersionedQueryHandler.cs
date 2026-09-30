using Sergin.SharedKernel.Application.Commands.Queries;

namespace Sergin.SharedKernel.Application.Concurrency;

/// <summary>
/// Base for a GetOne handler over a versioned aggregate. The derived handler returns the read model with the
/// version it was read at; this class publishes that version to <see cref="ConcurrencyContext.Current"/> and
/// hands MediatR the bare response, so the version never lands on the response record and no handler can
/// forget to publish it.
/// </summary>
public abstract class VersionedQueryHandler<TQuery, TResponse>(ConcurrencyContext concurrency) : IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public abstract Task<ErrorOr<Versioned<TResponse>>> HandleVersioned(TQuery request, CancellationToken cancellationToken);

    public async Task<ErrorOr<TResponse>> Handle(TQuery request, CancellationToken cancellationToken)
    {
        ErrorOr<Versioned<TResponse>> result = await HandleVersioned(request, cancellationToken);

        return result
            .Then(v =>
            {
                concurrency.Current = v.Version;
                return v.Value;
            });
    }
}
