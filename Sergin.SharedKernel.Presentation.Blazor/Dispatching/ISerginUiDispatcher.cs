namespace Sergin.SharedKernel.Presentation.Blazor.Dispatching;

/// <summary>
/// Sends a MediatR request inside its own DI scope. In Blazor Server a "scoped" service lives for the
/// whole SignalR circuit, so resolving <see cref="ISender"/> straight off the circuit's provider would
/// share one DbContext across every interaction for the circuit's lifetime — producing an unbounded
/// change tracker, stale first-level-cache reads, and "a second operation was started on this context"
/// whenever two components render in parallel. Every send through this dispatcher gets a fresh scope,
/// i.e. exactly the lifetime an HTTP request gets in the Web API host.
/// </summary>
public interface ISerginUiDispatcher
{
    Task<ErrorOr<TResponse>> SendAsync<TResponse>(
        IRequest<ErrorOr<TResponse>> request, CancellationToken cancellationToken = default);
}
