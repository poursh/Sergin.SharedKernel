using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Presentation.Grpc.Dispatching;

namespace Sergin.SharedKernel.Presentation.Blazor.Dispatching;

/// <summary>
/// Replaces ScopedSerginUiDispatcher. Every send opens one fresh DI scope (same reasoning as before:
/// Blazor Server "scoped" is the whole SignalR circuit, not a request) and runs a permission check
/// against IUserContext before branching Local (ISender.Send, in-process) or Remote (IRemoteInvoker,
/// over gRPC). The permission check runs unconditionally, not just for Remote: Local mode already
/// re-checks it inside MediatR's PermissionCheckPipelineBehavior, so this is a deliberate, cheap
/// redundancy — see spec §5 — not a correctness gap for Local.
/// </summary>
internal sealed class RoutingSerginUiDispatcher(
    IServiceScopeFactory scopeFactory,
    IDispatchRouteResolver routeResolver) : ISerginUiDispatcher
{
    private static readonly ConcurrentDictionary<(Type Request, Type Response), Type> invokerTypeCache = new();

    public async Task<ErrorOr<TResponse>> SendAsync<TResponse>(
        IRequest<ErrorOr<TResponse>> request, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        IUserContext userContext = scope.ServiceProvider.GetRequiredService<IUserContext>();

        RequiredPermissionsAttribute? attribute =
            request.GetType().GetCustomAttribute<RequiredPermissionsAttribute>();

        if (attribute is not null && !userContext.HasPermission(attribute.Permissionas))
        {
            return Error.Forbidden();
        }

        Type requestType = request.GetType();

        if (routeResolver.IsRemote(requestType))
        {
            Type invokerType = invokerTypeCache.GetOrAdd(
                (requestType, typeof(TResponse)),
                key => typeof(IRemoteInvoker<,>).MakeGenericType(key.Request, key.Response));

            dynamic invoker = scope.ServiceProvider.GetRequiredService(invokerType);
            return await invoker.InvokeAsync((dynamic)request, cancellationToken);
        }

        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(request, cancellationToken);
    }
}
