using System.Security.Claims;

namespace Sergin.SharedKernel.Application.Securities.Users;

/// <summary>
/// An <see cref="IUserContext"/> read entirely from an authenticated <see cref="ClaimsPrincipal"/>.
/// </summary>
/// <remarks>
/// Permissions come from <see cref="SerginClaimTypes.Permission"/> claims stamped at sign-in, so
/// resolving a user context costs no database work — see <see cref="IExternalIdentityResolver"/> for
/// where they are put there. An unauthenticated principal yields <see cref="Anonymous"/> rather than
/// throwing: services are resolved during the OIDC callback before sign-in completes, and throwing
/// there would fail the login instead of the authorization check.
/// </remarks>
public sealed class ClaimsPrincipalUserContext : IUserContext
{
    private static readonly HashSet<Permission> noPermissions = [];

    private ClaimsPrincipalUserContext(
        UserId id,
        string userName,
        string email,
        string firstName,
        string lastName,
        HashSet<Permission> permissions)
    {
        Id = id;
        UserName = userName;
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        Permissions = permissions;
    }

    /// <summary>No identity, no rights. Every permission check against this context fails.</summary>
    public static ClaimsPrincipalUserContext Anonymous { get; } = new(
        new UserId(Guid.Empty),
        "ANONYMOUS",
        string.Empty,
        string.Empty,
        string.Empty,
        noPermissions);

    public UserId Id { get; }

    public string UserName { get; }

    public string FirstName { get; }

    public string LastName { get; }

    public string Email { get; }

    public HashSet<Permission> Permissions { get; }

    public static ClaimsPrincipalUserContext Create(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return Anonymous;
        }

        HashSet<Permission> permissions = [];

        foreach (Claim claim in principal.FindAll(SerginClaimTypes.Permission))
        {
            // A permission claim that no longer parses means the stamped set predates a format change.
            // Dropping it degrades the caller to fewer rights, which is the safe direction.
            if (Permission.TryCreate(claim.Value, out Permission? permission))
            {
                permissions.Add(permission);
            }
        }

        return new ClaimsPrincipalUserContext(
            new UserId(ReadUserId(principal)),
            ReadClaim(principal, ClaimTypes.Name, "preferred_username"),
            ReadClaim(principal, ClaimTypes.Email, "email"),
            ReadClaim(principal, ClaimTypes.GivenName, "given_name"),
            ReadClaim(principal, ClaimTypes.Surname, "family_name"),
            permissions);
    }

    private static Guid ReadUserId(ClaimsPrincipal principal)
        => Guid.TryParse(FindValue(principal, SerginClaimTypes.UserId), out Guid id) ? id : Guid.Empty;

    private static string ReadClaim(ClaimsPrincipal principal, string primaryType, string fallbackType)
        => FindValue(principal, primaryType) ?? FindValue(principal, fallbackType) ?? string.Empty;

    private static string? FindValue(ClaimsPrincipal principal, string claimType)
        => principal.FindFirst(claimType)?.Value;
}
