namespace Sergin.SharedKernel.Application.Dispatching;

/// <summary>
/// Sends a MediatR request through a fresh DI scope, with a permission pre-check and Local/Remote
/// routing applied uniformly across every presentation adapter (Blazor pages, WebApi endpoints).
/// The Blazor-circuit-lifetime rationale that originally motivated the fresh-scope-per-call behavior
/// still applies to Blazor; WebApi callers get the same scope-per-call shape for free, at the cost of
/// one extra, immediately-disposed scope per call under a host where the framework already scopes
/// correctly per request.
/// </summary>
public interface ISerginSender
{
    Task<ErrorOr<TResponse>> SendAsync<TResponse>(
        IRequest<ErrorOr<TResponse>> request, CancellationToken cancellationToken = default);
}
