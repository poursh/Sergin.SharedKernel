using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Domain.Securities;
using Sergin.SharedKernel.Application.Securities.Users;

namespace Sergin.SharedKernel.Application.Securities.Authorization;

internal sealed class ScopeAuthorization(
    IUserContext userContext,
    IScopedPermissionChecker checker,
    IAuthorizationScopeContext scopeContext,
    ILogger<ScopeAuthorization> logger) : IScopeAuthorization
{
    public Task<bool> CurrentUserHasAsync(Permission permission, CancellationToken cancellationToken = default) =>
        CurrentUserHasAsync(permission, scopeContext.ScopeId, cancellationToken);

    public async Task<bool> CurrentUserHasAsync(Permission permission, Guid scopeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permission);

        // Answered by scope only. A system identity (the outbox relay,
        // which has no user row) is the one caller that skips the lookup.
        if (userContext is ISystemUserContext)
        {
            return true;
        }

        try
        {
            return await checker.HasPermissionAsync(userContext.Id.Value, permission, scopeId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Deny by default: a check that cannot be answered never grants.
            logger.LogError(exception, "Scoped permission check for {Permission} on scope {ScopeId} failed; denying.", permission.Value, scopeId);
            return false;
        }
    }

    public async Task<ScopeReach> CurrentUserReachAsync(Permission permission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permission);

        if (userContext is ISystemUserContext)
        {
            return ScopeReach.Everything;
        }

        try
        {
            return await checker.GetReachAsync(userContext.Id.Value, permission, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A reach that cannot be answered reaches nothing, as a failed check grants nothing.
            logger.LogError(exception, "Scope reach lookup for {Permission} failed; reaching nothing.", permission.Value);
            return ScopeReach.Nothing;
        }
    }

    public async Task<bool> CurrentUserHoldsAnywhereAsync(Permission permission, CancellationToken cancellationToken = default)
    {
        ScopeReach reach = await CurrentUserReachAsync(permission, cancellationToken);

        return reach.ReachesAnywhere;
    }
}
