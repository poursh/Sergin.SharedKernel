namespace Sergin.SharedKernel.Application.Securities.Authorization;

/// <summary>
/// The signed-in user's scoped permission check, answered by scope only: every check is asked of
/// <see cref="IScopedPermissionChecker"/> with the user's id, and the <c>sergin:permission</c> claims are not
/// consulted. Only a system admin (<see cref="Users.IUserContext.IsSystemAdmin"/>) passes without a lookup. Any
/// error in the check is a denial.
/// </summary>
public interface IScopeAuthorization
{
    /// <summary>Checks <paramref name="permission"/> on <paramref name="scopeId"/>: the target the caller names.</summary>
    Task<bool> CurrentUserHasAsync(Permission permission, Guid scopeId, CancellationToken cancellationToken = default);

    /// <summary>Checks <paramref name="permission"/> on the scope <see cref="IAuthorizationScopeContext"/> holds.</summary>
    Task<bool> CurrentUserHasAsync(Permission permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every scope the user holds <paramref name="permission"/> on: <see cref="ScopeReach.Everything"/> for a system
    /// admin, <see cref="ScopeReach.Nothing"/> when the lookup fails.
    /// </summary>
    Task<ScopeReach> CurrentUserReachAsync(Permission permission, CancellationToken cancellationToken = default);
}
