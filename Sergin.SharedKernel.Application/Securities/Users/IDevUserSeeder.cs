namespace Sergin.SharedKernel.Application.Securities.Users;

/// <summary>
/// Makes the configured development user (<c>Sergin:DevUser</c>) a real user of the module that owns the user
/// store, holding a global administrator grant. Scoped checks answer from that store by user id, not from the
/// configured permissions, so without it a DevUser host would be refused on every scoped check.
/// </summary>
/// <remarks>
/// Called once at start by the Blazor bootstrap, in DevUser mode only — which is Development only. Optional: a host
/// composing no user store registers none and skips the step. Must be idempotent.
/// </remarks>
public interface IDevUserSeeder
{
    Task EnsureAsync(DevUser user, CancellationToken cancellationToken);
}

/// <summary>The configured development user.</summary>
public sealed record DevUser(Guid Id, string UserName, string Email, string FirstName, string LastName);
