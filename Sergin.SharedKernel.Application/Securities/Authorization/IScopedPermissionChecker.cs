namespace Sergin.SharedKernel.Application.Securities.Authorization;

/// <summary>
/// Answers whether a user holds a permission on a target scope, from every active assignment the user has.
/// Implemented by the module that owns the scope tree (UserAccess); a host without one gets
/// <see cref="DenyAllScopedPermissionChecker"/>, so a scoped check fails closed rather than going unanswered.
/// Callers acting for the signed-in user go through <see cref="IScopeAuthorization"/>, which lets a system identity
/// through without a lookup and turns any failure into a denial.
/// </summary>
public interface IScopedPermissionChecker
{
    Task<bool> HasPermissionAsync(Guid userId, Permission permission, Guid scopeId, CancellationToken cancellationToken = default);

    /// <summary>Every scope the user's active grants reach with <paramref name="permission"/>, for the read filter.</summary>
    Task<ScopeReach> GetReachAsync(Guid userId, Permission permission, CancellationToken cancellationToken = default);
}
