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

        // Answered by scope only: the global claims are not consulted. A system admin (the outbox relay identity,
        // which has no user row) is the one caller that skips the lookup.
        if (userContext.IsSystemAdmin)
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
}
