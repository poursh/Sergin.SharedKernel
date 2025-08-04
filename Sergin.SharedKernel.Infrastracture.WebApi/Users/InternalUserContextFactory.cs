using Microsoft.AspNetCore.Http;
using Sergin.SharedKernel.Application.Securities;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Domain.Users;

namespace Sergin.SharedKernel.Infrastracture.WebApi.Users;

internal sealed record InternalUserContext(UserId Id, string UserName, string Email, string FirstName, string LastName, HashSet<Permission> Permissions) : IUserContext
{
    public static readonly InternalUserContext SystemUser = new(
        new UserId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
        "SYSTEM",
        string.Empty,
        string.Empty,
        string.Empty,
        [Permission.AllPlatform]);

    public static readonly InternalUserContext AnonymousUser = new(
        new UserId(Guid.Empty),
        "ANONYMOUS",
        string.Empty,
        string.Empty,
        string.Empty,
        []);
}

internal sealed class InternalUserContextFactory(IHttpContextAccessor httpContextAccessor) : IUserContextFactory
{
    public IUserContext CreateUserContext()
        => httpContextAccessor.HttpContext switch
        {
            null => InternalUserContext.SystemUser,
            _ => InternalUserContext.AnonymousUser
            //{ User: { } user } when user.GetUserId() is null => InternalUserContext.AnonymousUser,
            //{ User: { } user } => new CurrentUser(
            //    user.GetUserId()!.Value,
            //    user.GetUsername()!,
            //    user.GetEmail()!,
            //    user.GetFirstName()!,
            //    user.GetLastName()!,
            //    user.GetPermissions()!),
        };
}
