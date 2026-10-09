namespace Sergin.SharedKernel.Application.Securities.Authorization;

/// <summary>
/// The signed-in user's scoped permission check. A permission the user holds globally (the
/// <c>sergin:permission</c> claims, which carry only grants effective on every scope) passes without a lookup;
/// anything else is asked of <see cref="IScopedPermissionChecker"/>. Any error in the check is a denial.
/// </summary>
public interface IScopeAuthorization
{
    Task<bool> CurrentUserHasAsync(Permission permission, Guid scopeId, CancellationToken cancellationToken = default);
}
