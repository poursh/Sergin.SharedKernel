namespace Sergin.SharedKernel.Presentation.Grpc.Dispatching;

/// <summary>
/// Client-side stub for a request whose handler runs in another process. Implemented once per feature
/// (one per rpc method), the same "one interface per feature" shape as every other Sergin query
/// repository interface. Not a second entry point into Application — see DeviceGrpcService (the
/// server-side counterpart, added in the DeviceManagement module) for why: it still ends in
/// ISender.Send.
/// </summary>
public interface IRemoteInvoker<TRequest, TResponse>
    where TRequest : IRequest<ErrorOr<TResponse>>
{
    Task<ErrorOr<TResponse>> InvokeAsync(TRequest request, CancellationToken cancellationToken);
}
