namespace Sergin.SharedKernel.Application.Securities.Users;

/// <summary>
/// Claim types Sergin stamps onto the authenticated principal itself, rather than reading from the
/// identity provider. Keycloak proves who the caller is; these carry what Sergin decided they may do.
/// </summary>
public static class SerginClaimTypes
{
    /// <summary>The Sergin-side user id, which is not the identity provider's subject.</summary>
    public const string UserId = "sergin:user_id";

    /// <summary>One claim per <see cref="Permission"/> the user holds. Never a single joined value.</summary>
    public const string Permission = "sergin:permission";
}
