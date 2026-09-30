using System.Reflection;
using MediatR;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Securities.Users;

namespace Sergin.SharedKernel.Application.Securities.Authorization;

internal sealed class PermissionCheckPipelineBehavior<TRequest, TResponse>(
    IUserContext userContext) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        RequiredPermissionsAttribute? att = request.GetType().GetCustomAttribute<RequiredPermissionsAttribute>();

        if (att is null || userContext.HasPermission(att.Permissionas))
        {
            return await next(cancellationToken);
        }

        if (ErrorOrResponse.TryFrom(Error.Forbidden(), out TResponse forbidden))
        {
            return forbidden;
        }

        throw new ForbiddenException();
    }
}
