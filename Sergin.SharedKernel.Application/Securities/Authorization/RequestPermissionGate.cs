using Sergin.SharedKernel.Application.Commands.Configuration;

namespace Sergin.SharedKernel.Application.Securities.Authorization;

/// <summary>
/// The permission check a request's settings ask for, and what passing it sets for the request's work. Every permission
/// the settings require is checked on the scope <see cref="IAuthorizationScopeContext"/> holds, or, for
/// <see cref="PermissionScope.Any"/>, on any scope. Once it passes, the permissions are published in
/// <see cref="RequiredPermissionsContext"/> and <see cref="ScopeFilter"/> narrows scoped reads to the current subtree and
/// to where the caller holds every one of them, until the returned handle is disposed.
/// <para>
/// <c>PermissionCheckPipelineBehavior</c> enters it for every request; code that runs a request's own validator outside
/// the pipeline (starting a background command) enters it with that request's settings, so the validator reads what it
/// would read in the pipeline.
/// </para>
/// </summary>
internal sealed class RequestPermissionGate(
    IScopeAuthorization scopeAuthorization,
    IAuthorizationScopeContext scopeContext,
    IScopePathResolver scopePaths,
    RequiredPermissionsContext requiredPermissions,
    ScopeFilter scopeFilter)
{
    /// <summary>
    /// Checks <paramref name="settings"/>' permissions and, when they pass, publishes them and their reach until the
    /// returned handle is disposed; null when the caller is refused. A request that requires no permission passes with
    /// nothing published: no record check on its save, and the scope filter's fail-closed default for its reads.
    /// </summary>
    public async Task<IDisposable?> EnterAsync(CommandSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.RequiredPermissions.Count == 0)
        {
            return Nothing.Instance;
        }

        Guid scopeId = scopeContext.ScopeId;

        if (settings.PermissionScope == PermissionScope.Current
            && !await IsAllowedAsync(settings.RequiredPermissions, scopeId, cancellationToken))
        {
            return null;
        }

        // Each permission's reach is looked up once: it answers PermissionScope.Any and narrows the reads.
        IReadOnlyCollection<ScopeReach> reaches = await ReachesAsync(settings.RequiredPermissions, cancellationToken);

        if (settings.PermissionScope == PermissionScope.Any && !reaches.All(one => one.ReachesAnywhere))
        {
            return null;
        }

        string? currentPath = await scopePaths.GetPathAsync(scopeId, cancellationToken);
        ScopeReach reach = reaches.Aggregate(ScopeReach.Everything, (all, one) => all.Intersect(one));

        IDisposable published = requiredPermissions.Publish(settings.RequiredPermissions);
        IDisposable filtered = scopeFilter.Apply(currentPath, reach);

        return new Both(filtered, published);
    }

    private async Task<bool> IsAllowedAsync(IReadOnlyCollection<PermissionDefinition> permissions, Guid scopeId, CancellationToken cancellationToken)
    {
        foreach (PermissionDefinition permission in permissions)
        {
            if (!await scopeAuthorization.CurrentUserHasAsync(permission, scopeId, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<IReadOnlyCollection<ScopeReach>> ReachesAsync(IReadOnlyCollection<PermissionDefinition> permissions, CancellationToken cancellationToken)
    {
        List<ScopeReach> reaches = [];

        foreach (PermissionDefinition permission in permissions)
        {
            reaches.Add(await scopeAuthorization.CurrentUserReachAsync(permission, cancellationToken));
        }

        return reaches;
    }

    private sealed class Nothing : IDisposable
    {
        public static Nothing Instance { get; } = new();

        public void Dispose()
        {
            // Nothing was published.
        }
    }

    /// <summary>Restores the filter, then the permissions: the reverse of the order they were set.</summary>
    private sealed class Both(IDisposable first, IDisposable second) : IDisposable
    {
        public void Dispose()
        {
            first.Dispose();
            second.Dispose();
        }
    }
}
