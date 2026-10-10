using MediatR;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Configuration;
using Sergin.SharedKernel.Application.Securities.Users;

namespace Sergin.SharedKernel.Application.Securities.Authorization;

internal sealed class PermissionCheckPipelineBehavior<TRequest, TResponse>(
    IUserContext userContext,
    IScopeAuthorization scopeAuthorization,
    CommandConfigurationRegistry configurations) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        CommandSettings settings = configurations.For(request.GetType());

        if (await IsAllowedAsync(settings, cancellationToken))
        {
            return await next(cancellationToken);
        }

        if (ErrorOrResponse.TryFrom(Error.Forbidden(), out TResponse forbidden))
        {
            return forbidden;
        }

        throw new ForbiddenException();
    }

    private async Task<bool> IsAllowedAsync(CommandSettings settings, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<Permission> required = settings.RequiredPermissions;

        if (required.Count == 0)
        {
            return true;
        }

        if (!settings.PermissionsInCurrentScope)
        {
            return userContext.HasPermission([.. required]);
        }

        // Opted in: each permission on the scope IAuthorizationScopeContext holds. Global claims still pass first.
        foreach (Permission permission in required)
        {
            if (!await scopeAuthorization.CurrentUserHasAsync(permission, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }
}
