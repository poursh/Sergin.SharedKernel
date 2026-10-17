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

    /// <summary>
    /// True only for a system identity with no user row, such as the outbox relay. It passes every permission
    /// check without a lookup. A signed-in user is never one; a user granted <c>AllPlatform</c> is answered by scope
    /// like any other grant.
    /// </summary>
    bool IsSystemAdmin => false;
}
