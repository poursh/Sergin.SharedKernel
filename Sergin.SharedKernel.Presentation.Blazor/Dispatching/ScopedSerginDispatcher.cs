using Microsoft.Extensions.DependencyInjection;

namespace Sergin.SharedKernel.Presentation.Blazor.Dispatching;

/// <summary>
/// Opens one fresh DI scope per send. In Blazor Server, "scoped" is the whole SignalR circuit's
/// lifetime (as long as the user's tab stays open), not a single operation's — resolving ISender
/// straight off the circuit's container would share one DbContext across every interaction, producing
/// an unbounded change tracker and "a second operation was started on this context" the moment two
/// components render concurrently. No permission pre-check and no Local/Remote branch here anymore:
/// both are now the MediatR pipeline's job (PermissionCheckPipelineBehavior covers every Send call,
/// Local or Remote, since a Remote request now resolves a real IRequestHandler too — see
/// RemoteForwardingHandler in Sergin.SharedKernel.Infrastructure).
/// </summary>
internal sealed class ScopedSerginDispatcher(IServiceScopeFactory scopeFactory) : ISerginDispatcher
{
    public async Task<ErrorOr<TResponse>> SendAsync<TResponse>(
        IRequest<ErrorOr<TResponse>> request, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(request, cancellationToken);
    }
}
