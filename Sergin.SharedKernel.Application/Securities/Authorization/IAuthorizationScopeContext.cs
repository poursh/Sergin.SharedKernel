namespace Sergin.SharedKernel.Application.Securities.Authorization;

/// <summary>
/// The scope the caller is working in: the target of every permission a command configuration requires (unless it
/// declares <c>AtRoot()</c>), and of <see cref="IScopeAuthorization.CurrentUserHasAsync(Permission, CancellationToken)"/>.
/// It is <see cref="AuthorizationScope.RootId"/> until code sets it: a scope picker for an explicit choice, or the
/// code that opens the scope when the system knows the target.
/// <para>
/// Registered scoped, next to <see cref="Sergin.SharedKernel.Application.Concurrency.ConcurrencyContext"/> and for the same reason. In Blazor the
/// scope is the circuit, and the dispatcher copies the circuit's value into each send's scope. WebApi takes it from
/// the <c>X-Sergin-Scope</c> header and gRPC from the <c>sergin-scope</c> metadata.
/// </para>
/// <para>
/// No existence check: a scope that does not exist grants nothing, so a bad value fails closed.
/// </para>
/// </summary>
public interface IAuthorizationScopeContext
{
    Guid ScopeId { get; }

    bool IsRoot { get; }

    void Set(Guid scopeId);

    void Reset();
}
