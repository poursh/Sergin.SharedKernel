using MediatR;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Configuration;
using Sergin.SharedKernel.Application.Securities.Users;

namespace Sergin.SharedKernel.Application.Securities.Authorization;

internal sealed class PermissionCheckPipelineBehavior<TRequest, TResponse>(
    IUserContext userContext,
    CommandConfigurationRegistry configurations) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<Permission> required = configurations.For(request.GetType()).RequiredPermissions;

        if (required.Count == 0 || userContext.HasPermission([.. required]))
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
