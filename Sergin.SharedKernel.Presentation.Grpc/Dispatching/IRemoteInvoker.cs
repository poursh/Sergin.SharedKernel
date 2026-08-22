namespace Sergin.SharedKernel.Presentation.Grpc.Dispatching;

/// <summary>
/// Client-side stub for a request whose handler runs in another process. Implemented once per feature
/// (one per rpc method), the same "one interface per feature" shape as every other Sergin query
/// repository interface. Not a second entry point into Application — see DeviceGrpcService (the
/// server-side counterpart, added in the DeviceManagement module) for why: it still ends in
/// ISender.Send.
/// </summary>
/// <remarks>
/// Identity/permission metadata propagation is not yet implemented on this interface. The design spec
/// (<c>docs/superpowers/specs/2026-08-21-dispatch-contract-design.md</c> §5) describes attaching the
/// caller's <c>UserId</c> and resolved <c>Permissions</c> to the gRPC call's metadata headers so the
/// remote side can log/assert against them; the shipped <c>RoutingSerginSender</c> resolves
/// <c>IUserContext</c> for its own permission gate but never forwards it here. Closing that gap requires
/// widening <see cref="InvokeAsync"/>'s signature to accept identity information — a breaking change for
/// every existing implementer (currently just <c>GetDeviceByIdGrpcInvoker</c>).
/// </remarks>
public interface IRemoteInvoker<TRequest, TResponse>
    where TRequest : IRequest<ErrorOr<TResponse>>
{
    Task<ErrorOr<TResponse>> InvokeAsync(TRequest request, CancellationToken cancellationToken);
}
