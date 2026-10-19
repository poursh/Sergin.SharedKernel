using MediatR;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Configuration;

namespace Sergin.SharedKernel.Application.Securities.Authorization;

/// <summary>
/// Checks every permission a request's configuration requires on the scope <see cref="IAuthorizationScopeContext"/>
/// holds, or, for a request bound to an aggregate that is not scoped (<see cref="PermissionScope.Any"/>), on any
/// scope: the caller only has to hold each permission somewhere. Once the check passes, the request runs
/// with two things set for it: its permissions in <see cref="RequiredPermissionsContext"/>, which the save checks
/// again on the scope of every record it writes, and <see cref="ScopeFilter"/>, which narrows its reads of scoped
/// aggregates to the current subtree and to where the caller holds every one of those permissions. The save's
/// <see cref="ForbiddenException"/> answers <c>Error.Forbidden()</c> like a refusal here.
/// </summary>
internal sealed class PermissionCheckPipelineBehavior<TRequest, TResponse>(
    IScopeAuthorization scopeAuthorization,
    IAuthorizationScopeContext scopeContext,
    IScopePathResolver scopePaths,
    RequiredPermissionsContext requiredPermissions,
    ScopeFilter scopeFilter,
    CommandConfigurationRegistry configurations) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        CommandSettings settings = configurations.For(request.GetType());

        // An unconfigured request runs with nothing published: no record check on its save, and the scope filter's
        // fail-closed default for its reads.
        if (settings.RequiredPermissions.Count == 0)
        {
            return await next(cancellationToken);
        }

        Guid scopeId = scopeContext.ScopeId;

        if (settings.PermissionScope == PermissionScope.Current
            && !await IsAllowedAsync(settings.RequiredPermissions, scopeId, cancellationToken))
        {
            return Forbidden();
        }

        // Each permission's reach is looked up once: it answers PermissionScope.Any and narrows the reads.
        IReadOnlyCollection<ScopeReach> reaches = await ReachesAsync(settings.RequiredPermissions, cancellationToken);

        if (settings.PermissionScope == PermissionScope.Any && !reaches.All(one => one.ReachesAnywhere))
        {
            return Forbidden();
        }

        string? currentPath = await scopePaths.GetPathAsync(scopeId, cancellationToken);
        ScopeReach reach = reaches.Aggregate(ScopeReach.Everything, (all, one) => all.Intersect(one));

        using IDisposable published = requiredPermissions.Publish(settings.RequiredPermissions);
        using IDisposable filtered = scopeFilter.Apply(currentPath, reach);

        TResponse forbidden = default!;

        try
        {
            return await next(cancellationToken);
        }
        catch (ForbiddenException) when (ErrorOrResponse.TryFrom(Error.Forbidden(), out forbidden))
        {
            return forbidden;
        }
    }

    private static TResponse Forbidden() =>
        ErrorOrResponse.TryFrom(Error.Forbidden(), out TResponse forbidden) ? forbidden : throw new ForbiddenException();

    private async Task<bool> IsAllowedAsync(IReadOnlyCollection<Permission> permissions, Guid scopeId, CancellationToken cancellationToken)
    {
        foreach (Permission permission in permissions)
        {
            if (!await scopeAuthorization.CurrentUserHasAsync(permission, scopeId, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<IReadOnlyCollection<ScopeReach>> ReachesAsync(IReadOnlyCollection<Permission> permissions, CancellationToken cancellationToken)
    {
        List<ScopeReach> reaches = [];

        foreach (Permission permission in permissions)
        {
            reaches.Add(await scopeAuthorization.CurrentUserReachAsync(permission, cancellationToken));
        }

        return reaches;
    }
}
