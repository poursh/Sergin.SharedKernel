namespace Sergin.SharedKernel.Application.Securities.Users;

/// <summary>
/// Carries an already-resolved <see cref="IUserContext"/> into a DI scope that has no way to build one.
/// </summary>
/// <remarks>
/// Blazor's dispatcher opens each send's scope from the <em>root</em> service provider, so that scope
/// has neither an <c>HttpContext</c> nor the circuit's <c>AuthenticationStateProvider</c> — a factory
/// resolved inside it would see an anonymous principal and every
/// <see cref="Authorization.RequiredPermissionsAttribute"/> check would fail. The dispatcher therefore
/// seeds <see cref="Current"/> on the child scope before resolving the sender, and
/// <c>AddSerginCore</c> prefers that value over calling <see cref="IUserContextFactory"/> again.
/// <para>
/// Registered scoped: one instance per scope, seeded at most once, never read across scopes.
/// </para>
/// </remarks>
public sealed class UserContextAccessor
{
    public IUserContext? Current { get; set; }
}
