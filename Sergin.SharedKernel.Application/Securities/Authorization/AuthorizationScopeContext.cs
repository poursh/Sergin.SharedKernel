namespace Sergin.SharedKernel.Application.Securities.Authorization;

internal sealed class AuthorizationScopeContext : IAuthorizationScopeContext
{
    public Guid ScopeId { get; private set; } = AuthorizationScope.RootId;

    public bool IsRoot => ScopeId == AuthorizationScope.RootId;

    public void Set(Guid scopeId)
    {
        if (scopeId == Guid.Empty)
        {
            throw new ArgumentException("A scope id must not be empty.", nameof(scopeId));
        }

        ScopeId = scopeId;
    }

    public void Reset() => ScopeId = AuthorizationScope.RootId;
}
