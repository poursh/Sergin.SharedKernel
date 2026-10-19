namespace Sergin.SharedKernel.Application.Securities.Authorization;

/// <summary>
/// The scopes the signed-in user can work in, by name, for the shell's scope picker. Implemented by the module that
/// owns the scope tree (UserAccess); a host without one gets <see cref="RootOnlyScopeDirectory"/>.
/// </summary>
/// <remarks>
/// A scope is listed when any active grant of the user reaches it, whatever the grant's permissions: the picker
/// offers where the user may work, not what they may do there. It is not a permission check and grants nothing.
/// </remarks>
public interface IScopeDirectory
{
    /// <summary>
    /// The live scopes the current user's active grants reach, root first and siblings by name. A listed scope whose
    /// parent is not listed has a null <see cref="ScopeOption.ParentId"/>. Empty for a user with no active grant.
    /// </summary>
    Task<IReadOnlyList<ScopeOption>> GetReachableAsync(CancellationToken cancellationToken = default);
}

/// <summary>One scope the user can pick, with its parent among the listed scopes and its depth in the whole tree.</summary>
public sealed record ScopeOption(Guid Id, Guid? ParentId, string Name, int Depth)
{
    /// <summary>
    /// The scope a user starts in when nothing else was chosen: the shallowest listed scope, so the root for anyone
    /// it is listed for, ties broken by name. Null for an empty list.
    /// </summary>
    public static ScopeOption? HomeOf(IEnumerable<ScopeOption> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options
            .OrderBy(option => option.Depth)
            .ThenBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Id)
            .FirstOrDefault();
    }
}

/// <summary>The fallback <see cref="IScopeDirectory"/>: the root alone, the only scope a host without a scope tree has.</summary>
public sealed class RootOnlyScopeDirectory : IScopeDirectory
{
    private static readonly IReadOnlyList<ScopeOption> Root = [new ScopeOption(AuthorizationScope.RootId, null, "Root", 1)];

    public Task<IReadOnlyList<ScopeOption>> GetReachableAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Root);
}
