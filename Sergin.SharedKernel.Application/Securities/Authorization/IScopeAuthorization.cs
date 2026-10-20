namespace Sergin.SharedKernel.Application.Securities.Authorization;

/// <summary>
/// The signed-in user's scoped permission check, answered by scope only: every check is asked of
/// <see cref="IScopedPermissionChecker"/> with the user's id; <see cref="Users.IUserContext"/> carries no
/// permissions. Only a system identity (<see cref="Users.ISystemUserContext"/>) passes without a lookup. Any
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
    /// identity, <see cref="ScopeReach.Nothing"/> when the lookup fails.
    /// </summary>
    Task<ScopeReach> CurrentUserReachAsync(Permission permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the user holds <paramref name="permission"/> on any scope at all: the check for data that belongs to
    /// no scope. True for a system identity, false when the lookup fails.
    /// </summary>
    Task<bool> CurrentUserHoldsAnywhereAsync(Permission permission, CancellationToken cancellationToken = default);

    /// <summary>The <see cref="PermissionDefinition"/> form of <see cref="CurrentUserHasAsync(Permission, Guid, CancellationToken)"/>.</summary>
    Task<bool> CurrentUserHasAsync(PermissionDefinition permission, Guid scopeId, CancellationToken cancellationToken = default);

    /// <summary>The <see cref="PermissionDefinition"/> form of <see cref="CurrentUserHasAsync(Permission, CancellationToken)"/>.</summary>
    Task<bool> CurrentUserHasAsync(PermissionDefinition permission, CancellationToken cancellationToken = default);

    /// <summary>The <see cref="PermissionDefinition"/> form of <see cref="CurrentUserReachAsync(Permission, CancellationToken)"/>.</summary>
    Task<ScopeReach> CurrentUserReachAsync(PermissionDefinition permission, CancellationToken cancellationToken = default);

    /// <summary>The <see cref="PermissionDefinition"/> form of <see cref="CurrentUserHoldsAnywhereAsync(Permission, CancellationToken)"/>.</summary>
    Task<bool> CurrentUserHoldsAnywhereAsync(PermissionDefinition permission, CancellationToken cancellationToken = default);
}
