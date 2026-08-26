namespace Sergin.SharedKernel.Application.Securities.Users;

/// <summary>
/// Turns an identity proven by an external provider into a Sergin user with a permission set.
/// </summary>
/// <remarks>
/// This is the seam between authentication and authorization. SharedKernel knows how to authenticate
/// (OIDC against Keycloak) but owns no user store, so a module implements this interface and registers
/// it in its own <c>AddServices</c>; a host that enables <c>Sergin:Auth:Mode=Keycloak</c> without any
/// module supplying an implementation fails at startup rather than signing everyone in with no rights.
/// <para>
/// Implementations are called from the OIDC <c>OnTokenValidated</c> event, which runs <em>before</em>
/// sign-in completes: the ambient <see cref="IUserContext"/> is anonymous at that moment, so a resolver
/// that dispatches through MediatR must not send a request carrying
/// <see cref="Authorization.RequiredPermissionsAttribute"/>.
/// </para>
/// </remarks>
public interface IExternalIdentityResolver
{
    Task<ExternalIdentityResult> ResolveAsync(ExternalIdentity identity, CancellationToken cancellationToken);
}

/// <summary>What the identity provider asserted about the caller.</summary>
/// <param name="Subject">The provider's stable subject identifier — Keycloak's <c>sub</c>.</param>
public sealed record ExternalIdentity(
    string Subject,
    string UserName,
    string Email,
    string FirstName,
    string LastName);

/// <summary>What Sergin decided about that caller.</summary>
public sealed record ExternalIdentityResult(Guid UserId, IReadOnlyCollection<Permission> Permissions);
