using Grpc.Core;
using Grpc.Core.Interceptors;
using Sergin.SharedKernel.Application.Securities.Authorization;

namespace Sergin.SharedKernel.Presentation.Grpc.Authorization;

/// <summary>
/// The serving host's half: seeds the call scope's <see cref="IAuthorizationScopeContext"/> from request metadata
/// before the service sends into its pipeline. Register with
/// <c>AddGrpc(o =&gt; o.Interceptors.Add&lt;AuthorizationScopeServerInterceptor&gt;())</c>; Grpc.AspNetCore builds
/// it from the call's scope, so it shares the context the service's ISender uses.
/// </summary>
public sealed class AuthorizationScopeServerInterceptor(IAuthorizationScopeContext scopeContext) : Interceptor
{
    public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(continuation);

        if (context.RequestHeaders.GetValue(AuthorizationScopeMetadata.ScopeKey) is { } sent)
        {
            if (!Guid.TryParse(sent, out Guid scopeId) || scopeId == Guid.Empty)
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument, $"{AuthorizationScopeMetadata.ScopeKey} must be a non-empty GUID."));
            }

            scopeContext.Set(scopeId);
        }

        return continuation(request, context);
    }
}
