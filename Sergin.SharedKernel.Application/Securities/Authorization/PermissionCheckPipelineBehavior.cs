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

        if (typeof(TResponse) == typeof(IErrorOr))
        {
            return (TResponse)(object)Error.Forbidden();
        }

        if (typeof(TResponse).IsGenericType &&
            typeof(TResponse).GetGenericTypeDefinition() == typeof(ErrorOr<>))
        {
            Type resultType = typeof(TResponse).GetGenericArguments()[0];

            MethodInfo fromMethod = typeof(ErrorOr<>)
                .MakeGenericType(resultType)
                .GetMethod(nameof(ErrorOr<object>.From));

            return (TResponse)fromMethod!.Invoke(null, [new List<Error>([Error.Forbidden()])]);

        }

        throw new ForbiddenException();
    }
}
