namespace Sergin.SharedKernel.Application.Securities.Users;

/// <summary>
/// Claim types Sergin stamps onto the authenticated principal itself, rather than reading from the
/// identity provider. Keycloak proves who the caller is; these say who that is in Sergin's terms.
/// </summary>
public static class SerginClaimTypes
{
    /// <summary>The Sergin-side user id, which is not the identity provider's subject.</summary>
    public const string UserId = "sergin:user_id";
}
