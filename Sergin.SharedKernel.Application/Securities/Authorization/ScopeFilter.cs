using Sergin.SharedKernel.Application.Securities.Users;

namespace Sergin.SharedKernel.Application.Securities.Authorization;

/// <summary>
/// What the request being handled may read of a scoped aggregate: rows inside <see cref="CurrentPath"/>'s subtree
/// that the caller's reach covers. <c>PermissionCheckPipelineBehavior</c> fills it from the request's required
/// permissions; the EF query filter and every raw-SQL read of a scoped table apply it.
/// <para>
/// It fails closed. Until a configured request fills it, a system identity (<see cref="ISystemUserContext"/>, such as the outbox relay) sees
/// everything and anyone else sees nothing, so a request that requires no permission reads no scoped row.
/// </para>
/// <para>Registered scoped, next to <see cref="RequiredPermissionsContext"/>.</para>
/// </summary>
public sealed class ScopeFilter(IUserContext user)
{
    private Values? values;

    private Values Current => values ??= user is ISystemUserContext
        ? new Values(AuthorizationScope.RootPath, ScopeReach.Everything)
        : new Values(null, ScopeReach.Nothing);

    /// <summary>False when nothing at all is visible: the current scope is unknown.</summary>
    public bool Visible => Current.CurrentPath is not null;

    /// <summary>The current scope's path; the root's when <see cref="Visible"/> is false, so it is always a valid ltree.</summary>
    public string CurrentPath => Current.CurrentPath ?? AuthorizationScope.RootPath;

    /// <summary>The reach sets no limit: a system identity.</summary>
    public bool Unrestricted => Current.Reach.Unrestricted;

    /// <summary>The paths of every grant that covers its subtree.</summary>
    public IReadOnlyList<string> SubtreePaths =>
        [.. Current.Reach.Entries.Where(entry => entry.IncludeSubtree).Select(entry => entry.Path)];

    /// <summary>The scopes of every grant that covers its own scope only.</summary>
    public IReadOnlyList<Guid> ExactScopeIds =>
        [.. Current.Reach.Entries.Where(entry => !entry.IncludeSubtree).Select(entry => entry.ScopeId)];

    /// <summary>
    /// Narrows reads to <paramref name="currentPath"/>'s subtree and <paramref name="reach"/> until the returned
    /// handle is disposed, which restores the previous value. A null path is an unknown scope: nothing is visible.
    /// </summary>
    public IDisposable Apply(string? currentPath, ScopeReach reach)
    {
        ArgumentNullException.ThrowIfNull(reach);

        Restore restore = new(this, values);
        values = new Values(currentPath, reach);
        return restore;
    }

    private sealed record Values(string? CurrentPath, ScopeReach Reach);

    private sealed class Restore(ScopeFilter filter, Values? previous) : IDisposable
    {
        public void Dispose() => filter.values = previous;
    }
}
