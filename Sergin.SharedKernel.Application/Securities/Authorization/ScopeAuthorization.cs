using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Domain.Securities;
using Sergin.SharedKernel.Application.Securities.Users;

namespace Sergin.SharedKernel.Application.Securities.Authorization;

internal sealed class ScopeAuthorization(
    IUserContext userContext,
    IScopedPermissionChecker checker,
    IAuthorizationScopeContext scopeContext,
    PermissionCatalog catalog,
    ILogger<ScopeAuthorization> logger) : IScopeAuthorization
{
    public Task<bool> CurrentUserHasAsync(PermissionDefinition permission, CancellationToken cancellationToken = default) =>
        CurrentUserHasAsync(permission, scopeContext.ScopeId, cancellationToken);

    public async Task<bool> CurrentUserHasAsync(PermissionDefinition permission, Guid scopeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permission);

        // Answered by scope only. A system identity (the outbox relay,
        // which has no user row) is the one caller that skips the lookup.
        if (userContext is ISystemUserContext)
        {
            return true;
        }

        if (!IsRegistered(permission))
        {
            return false;
        }

        try
        {
            return await checker.HasPermissionAsync(userContext.Id.Value, permission.Permission, scopeId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Deny by default: a check that cannot be answered never grants.
            logger.LogError(exception, "Scoped permission check for {Permission} on scope {ScopeId} failed; denying.", permission.Permission.Value, scopeId);
            return false;
        }
    }

    public async Task<ScopeReach> CurrentUserReachAsync(PermissionDefinition permission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permission);

        if (userContext is ISystemUserContext)
        {
            return ScopeReach.Everything;
        }

        if (!IsRegistered(permission))
        {
            return ScopeReach.Nothing;
        }

        try
        {
            return await checker.GetReachAsync(userContext.Id.Value, permission.Permission, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A reach that cannot be answered reaches nothing, as a failed check grants nothing.
            logger.LogError(exception, "Scope reach lookup for {Permission} failed; reaching nothing.", permission.Permission.Value);
            return ScopeReach.Nothing;
        }
    }

    public async Task<bool> CurrentUserHoldsAnywhereAsync(PermissionDefinition permission, CancellationToken cancellationToken = default)
    {
        ScopeReach reach = await CurrentUserReachAsync(permission, cancellationToken);

        return reach.ReachesAnywhere;
    }

    /// <summary>
    /// A handler's own check cannot be scanned at start, so a definition no aggregate registered is caught here: no
    /// role can grant it, and answering from stored grants anyway would let a stale row decide. Fails closed.
    /// </summary>
    private bool IsRegistered(PermissionDefinition permission)
    {
        if (catalog.Contains(permission))
        {
            return true;
        }

        logger.LogWarning(
            "{Permission} is not registered by any aggregate's Permissions(...), so it is not in the permission catalogue; denying.",
            permission.Permission.Value);
        return false;
    }
}
