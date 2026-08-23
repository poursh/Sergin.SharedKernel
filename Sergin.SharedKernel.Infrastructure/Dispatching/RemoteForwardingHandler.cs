using MediatR;
using Sergin.SharedKernel.Presentation.Grpc.Dispatching;

namespace Sergin.SharedKernel.Infrastructure.Dispatching;

/// <summary>
/// Wraps an IRemoteInvoker as a real MediatR handler, so a Remote-configured module's requests flow
/// through the same ISender.Send pipeline a Local module's real handler would — PermissionCheckPipelineBehavior
/// and ValidationPipelineBehavior now cover Remote calls too, not just Local ones. Pure forwarding, no
/// logic of its own: the one place to add shared remote-call behavior later (retry, tracing) without
/// touching every feature. Registered explicitly per feature by a module's AddRemoteServices — never
/// discovered by MediatR's assembly scan, since it's generic and lives in a different assembly than any
/// module's ContractsAssembly. Public, not internal: every module's own AddRemoteServices
/// (a different assembly per module, e.g. DeviceManagement.Presentation.Grpc) names this type directly
/// in its registration call — an InternalsVisibleTo grant would need one entry per module adopting
/// remote dispatch, which doesn't scale the way this type's public API surface (IRemoteInvoker,
/// ISerginRemoteModule) already doesn't need to.
/// </summary>
public sealed class RemoteForwardingHandler<TRequest, TResponse>(IRemoteInvoker<TRequest, TResponse> invoker)
    : IRequestHandler<TRequest, ErrorOr<TResponse>>
    where TRequest : IRequest<ErrorOr<TResponse>>
{
    public Task<ErrorOr<TResponse>> Handle(TRequest request, CancellationToken cancellationToken)
        => invoker.InvokeAsync(request, cancellationToken);
}
