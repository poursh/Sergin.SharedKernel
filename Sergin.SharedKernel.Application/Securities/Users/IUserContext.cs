namespace Sergin.SharedKernel.Application.Securities.Users;

/// <summary>
/// Who the caller is. It carries no permissions: what the caller may do is answered by scope through
/// <see cref="Authorization.IScopeAuthorization"/>.
/// </summary>
public interface IUserContext
{
    UserId Id { get; }
    string UserName { get; }
    string FirstName { get; }
    string LastName { get; }
    string Email { get; }
}
