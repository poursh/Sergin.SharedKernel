using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Hosts.WebUi.Localizations;

/// <summary>
/// <c>GET /culture/set?culture=…&amp;redirectUri=…</c>: the language switch. It writes the culture cookie for a culture
/// the host knows (the default or a stored language) and redirects back with a full page load, since a Blazor circuit
/// fixes its culture when it starts. An unknown culture writes nothing; the redirect still happens. Only a local path
/// is followed; anything else goes to the site root. Whether the caller may actually use a draft language is the
/// culture provider's decision on the next request, not this endpoint's.
/// </summary>
internal static class CultureEndpoints
{
    public const string SetPath = "/culture/set";

    private static readonly TimeSpan cookieLifetime = TimeSpan.FromDays(365);

    public static IEndpointConventionBuilder MapSerginCultureSwitch(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(SetPath, SetCultureAsync).AllowAnonymous().ExcludeFromDescription();

    private static async Task<IResult> SetCultureAsync(
        string? culture,
        string? redirectUri,
        HttpContext httpContext,
        ITranslationCache cache,
        IOptions<SerginLocalizationOptions> options)
    {
        string defaultCulture = options.Value.DefaultCulture;

        await cache.EnsureLoadedAsync(defaultCulture, httpContext.RequestAborted).ConfigureAwait(false);

        if (KnownCulture(culture, defaultCulture, cache) is { } known)
        {
            httpContext.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(known)),
                new CookieOptions
                {
                    Path = "/",
                    MaxAge = cookieLifetime,
                    IsEssential = true,
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    // Browsers store a Secure cookie over plain HTTP on localhost, which is all local runs use;
                    // anywhere else this host is served over HTTPS (see the Keycloak notes on Secure cookies).
                    Secure = true,
                });
        }

        return Results.LocalRedirect(IsLocalPath(redirectUri) ? redirectUri : "/");
    }

    private static string? KnownCulture(string? culture, string defaultCulture, ITranslationCache cache)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return null;
        }

        if (string.Equals(culture, defaultCulture, StringComparison.OrdinalIgnoreCase))
        {
            return defaultCulture;
        }

        return cache.Languages
            .FirstOrDefault(language => string.Equals(language.Culture, culture, StringComparison.OrdinalIgnoreCase))
            ?.Culture;
    }

    /// <summary>A path on this site: one leading slash, not <c>//host</c> or <c>/\host</c>, no control characters.</summary>
    private static bool IsLocalPath([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? uri)
    {
        if (string.IsNullOrEmpty(uri) || uri[0] != '/')
        {
            return false;
        }

        if (uri.Length > 1 && uri[1] is '/' or '\\')
        {
            return false;
        }

        return !uri.Any(char.IsControl);
    }
}
