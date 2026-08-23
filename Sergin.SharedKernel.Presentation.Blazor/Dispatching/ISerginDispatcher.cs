namespace Sergin.SharedKernel.Presentation.Blazor.Dispatching;

public interface ISerginDispatcher
{
    Task<ErrorOr<TResponse>> SendAsync<TResponse>(
        IRequest<ErrorOr<TResponse>> request, CancellationToken cancellationToken = default);
}
