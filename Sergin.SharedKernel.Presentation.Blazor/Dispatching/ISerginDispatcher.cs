using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Presentation.Blazor.Dispatching;

public interface ISerginDispatcher
{
    Task<ErrorOr<TResponse>> SendAsync<TResponse>(
        IRequest<ErrorOr<TResponse>> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends <paramref name="request"/> with <paramref name="expected"/> as the version the caller last saw, and
    /// answers the version the send read or wrote. A detail page loads through this and hands the version back
    /// with its write, so a write made against a record someone else changed since is refused.
    /// </summary>
    Task<VersionedResult<TResponse>> SendVersionedAsync<TResponse>(
        IRequest<ErrorOr<TResponse>> request, RowVersion? expected = null, CancellationToken cancellationToken = default);
}
