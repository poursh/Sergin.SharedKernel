using Grpc.Core;
using Grpc.Core.Interceptors;
using Sergin.SharedKernel.Application.Securities.Authorization;

namespace Sergin.SharedKernel.Presentation.Grpc.Authorization;

/// <summary>
/// The caller's half of carrying the current scope over a Remote call. Put on the channel a module's invoker calls
/// through (<c>channel.Intercept(new AuthorizationScopeClientInterceptor(scopeContext))</c>, with the caller scope's
/// context): it sends a non-root <see cref="IAuthorizationScopeContext.ScopeId"/> as request metadata.
/// </summary>
public sealed class AuthorizationScopeClientInterceptor(IAuthorizationScopeContext scopeContext) : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        // A new Metadata, never the caller's own instance: see ConcurrencyClientInterceptor.
        Metadata headers = [];

        if (context.Options.Headers is { } existing)
        {
            foreach (Metadata.Entry entry in existing.Where(entry => entry.Key != AuthorizationScopeMetadata.ScopeKey))
            {
                headers.Add(entry);
            }
        }

        if (!scopeContext.IsRoot)
        {
            headers.Add(AuthorizationScopeMetadata.ScopeKey, scopeContext.ScopeId.ToString());
        }

        return continuation(
            request,
            new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, context.Options.WithHeaders(headers)));
    }
}
