
namespace Sergin.SharedKernel.Application.Securities.Users;

public interface IUserContext
{
    UserId Id { get; }
    string UserName { get; }
    string FirstName { get; }
    string LastName { get; }
    string Email { get; }

    bool IsSystemAdmin => Permissions?.Any(p => p == Permission.AllPlatform) ?? false;

    HashSet<Permission> Permissions { get; }

    bool HasPermission(params Permission[] permissions)
    { 
        return IsSystemAdmin || Permissions.Intersect(permissions).Count() == permissions.Length;
    }
}
