namespace Sergin.SharedKernel.Application.Securities.Authorization;

/// <summary>
/// The <c>ltree</c> path of a scope, for the code outside the module that owns the scope tree: a scoped record is
/// stamped with it, and reads are narrowed by it. Implemented by that module (UserAccess); a host without one gets
/// <see cref="UnknownScopePathResolver"/>, which knows the root only, so a record is never stamped with a scope
/// nothing can vouch for.
/// </summary>
public interface IScopePathResolver
{
    /// <summary>The live scope's path, root first; null for an unknown or deleted scope.</summary>
    Task<string?> GetPathAsync(Guid scopeId, CancellationToken cancellationToken = default);
}

/// <summary>The fallback <see cref="IScopePathResolver"/>: the root's path for the root, null for any other scope.</summary>
public sealed class UnknownScopePathResolver : IScopePathResolver
{
    public Task<string?> GetPathAsync(Guid scopeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(scopeId == AuthorizationScope.RootId ? AuthorizationScope.RootPath : null);
}
