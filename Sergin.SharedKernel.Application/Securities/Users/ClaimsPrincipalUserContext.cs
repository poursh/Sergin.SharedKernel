using System.Security.Claims;

namespace Sergin.SharedKernel.Application.Securities.Users;

/// <summary>
/// An <see cref="IUserContext"/> read entirely from an authenticated <see cref="ClaimsPrincipal"/>.
/// </summary>
/// <remarks>
/// The Sergin user id comes from the <see cref="SerginClaimTypes.UserId"/> claim stamped at sign-in, so
/// resolving a user context costs no database work — see <see cref="IExternalIdentityResolver"/> for
/// where it is put there. An unauthenticated principal yields <see cref="Anonymous"/> rather than
/// throwing: services are resolved during the OIDC callback before sign-in completes, and throwing
/// there would fail the login instead of the authorization check.
/// </remarks>
public sealed class ClaimsPrincipalUserContext : IUserContext
{
    private ClaimsPrincipalUserContext(
        UserId id,
        string userName,
        string email,
        string firstName,
        string lastName)
    {
        Id = id;
        UserName = userName;
        Email = email;
        FirstName = firstName;
        LastName = lastName;
    }

    /// <summary>No identity, so no grants. Every permission check against this context fails.</summary>
    public static ClaimsPrincipalUserContext Anonymous { get; } = new(
        new UserId(Guid.Empty),
        "ANONYMOUS",
        string.Empty,
        string.Empty,
        string.Empty);

    public UserId Id { get; }

    public string UserName { get; }

    public string FirstName { get; }

    public string LastName { get; }

    public string Email { get; }

    public static ClaimsPrincipalUserContext Create(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return Anonymous;
        }

        return new ClaimsPrincipalUserContext(
            new UserId(ReadUserId(principal)),
            ReadClaim(principal, ClaimTypes.Name, "preferred_username"),
            ReadClaim(principal, ClaimTypes.Email, "email"),
            ReadClaim(principal, ClaimTypes.GivenName, "given_name"),
            ReadClaim(principal, ClaimTypes.Surname, "family_name"));
    }

    private static Guid ReadUserId(ClaimsPrincipal principal)
        => Guid.TryParse(FindValue(principal, SerginClaimTypes.UserId), out Guid id) ? id : Guid.Empty;

    private static string ReadClaim(ClaimsPrincipal principal, string primaryType, string fallbackType)
        => FindValue(principal, primaryType) ?? FindValue(principal, fallbackType) ?? string.Empty;

    private static string? FindValue(ClaimsPrincipal principal, string claimType)
        => principal.FindFirst(claimType)?.Value;
}
