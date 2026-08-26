using Microsoft.Extensions.DependencyInjection;
using Sergin.SharedKernel.Application.Securities.Users;

namespace Sergin.SharedKernel.Presentation.Blazor.Dispatching;

/// <summary>
/// Opens one fresh DI scope per send. In Blazor Server, "scoped" is the whole SignalR circuit's
/// lifetime (as long as the user's tab stays open), not a single operation's — resolving ISender
/// straight off the circuit's container would share one DbContext across every interaction, producing
/// an unbounded change tracker and "a second operation was started on this context" the moment two
/// components render concurrently. No permission pre-check and no Local/Remote branch here anymore:
/// both are now the MediatR pipeline's job (PermissionCheckPipelineBehavior covers every Send call,
/// Local or Remote, since a Remote request now resolves a real IRequestHandler too — see
/// RemoteForwardingHandler in Sergin.SharedKernel.Presentation.Grpc).
/// </summary>
/// <remarks>
/// Registered <b>scoped</b>, not singleton. It was a singleton, correctly, while it held nothing but the
/// root <see cref="IServiceScopeFactory"/>. Real authentication gave it a second dependency that is
/// circuit-shaped: the scope this type creates comes from the <em>root</em> provider, so it has no
/// <c>HttpContext</c> and no <c>AuthenticationStateProvider</c>, and a user context built inside it would
/// be anonymous — every <c>[RequiredPermissions]</c> check would fail for a signed-in user. So the
/// dispatcher takes the caller's own <see cref="IUserContext"/> and seeds it into the child scope through
/// <see cref="UserContextAccessor"/>. Making this a singleton again reintroduces that bug.
/// </remarks>
internal sealed class ScopedSerginDispatcher(IServiceScopeFactory scopeFactory, IUserContext userContext)
    : ISerginDispatcher
{
    public async Task<ErrorOr<TResponse>> SendAsync<TResponse>(
        IRequest<ErrorOr<TResponse>> request, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<UserContextAccessor>().Current = userContext;

        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

        return await sender.Send(request, cancellationToken);
    }
}
