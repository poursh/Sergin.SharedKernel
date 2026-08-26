using Microsoft.AspNetCore.Http;
using Sergin.SharedKernel.Application.Securities.Users;

namespace Sergin.SharedKernel.Hosts.Authentication;

/// <summary>
/// Builds the user context from the current request's authenticated principal. Used by every host
/// running <see cref="SerginAuthMode.Keycloak"/>, API or UI alike, since both end up with the identity
/// on <c>HttpContext.User</c> — a cookie for the UI, a bearer token for the API.
/// </summary>
/// <remarks>
/// Returns an anonymous context when there is no request or no authenticated principal, rather than
/// throwing. Two situations depend on that: the OIDC callback resolves services before sign-in has
/// completed, and a Blazor circuit's own scope has no <c>HttpContext</c> at all — the latter is why
/// <see cref="UserContextAccessor"/> exists and why the dispatcher seeds it.
/// </remarks>
internal sealed class ClaimsPrincipalUserContextFactory(IHttpContextAccessor httpContextAccessor)
    : IUserContextFactory
{
    public IUserContext CreateUserContext()
        => ClaimsPrincipalUserContext.Create(httpContextAccessor.HttpContext?.User);
}
