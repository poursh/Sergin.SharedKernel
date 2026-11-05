using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Hosts.WebUi.Localizations;

/// <summary>
/// The only culture provider of the Blazor host, and the only gate between a draft language and a caller who may not
/// preview it (the cache holds drafts too). It reads the culture cookie and honours it when the culture is the default,
/// a published language, or a draft language the caller's <see cref="IDraftLanguagePreviewPolicy"/> allows; anything
/// else (a deleted or unpublished language, a revoked preview, a malformed cookie) answers nothing, so the request
/// quietly gets the default culture. The chosen culture, the default included, is loaded into the cache before the
/// request renders, because lookups are synchronous.
/// </summary>
/// <remarks>
/// A Blazor circuit takes its culture from the request that opens it, which passes through this provider too, so the
/// circuit and everything it dispatches (culture flows with the execution context) render in the same culture.
/// </remarks>
internal sealed class SerginCultureProvider(ITranslationCache cache, string defaultCulture) : RequestCultureProvider
{
    public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        CancellationToken cancellationToken = httpContext.RequestAborted;

        // Also loads the language list the first time, which the decision below reads.
        await cache.EnsureLoadedAsync(defaultCulture, cancellationToken).ConfigureAwait(false);

        string? requested = RequestedCulture(httpContext);

        if (requested is null || string.Equals(requested, defaultCulture, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        LanguageInfo? language = cache.Languages.FirstOrDefault(
            candidate => string.Equals(candidate.Culture, requested, StringComparison.OrdinalIgnoreCase));

        if (language is null || !await MayUseAsync(language, httpContext, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        await cache.EnsureLoadedAsync(language.Culture, cancellationToken).ConfigureAwait(false);

        return new ProviderCultureResult(language.Culture);
    }

    private static async Task<bool> MayUseAsync(LanguageInfo language, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (language.IsPublished)
        {
            return true;
        }

        IDraftLanguagePreviewPolicy? policy = httpContext.RequestServices.GetService<IDraftLanguagePreviewPolicy>();

        return policy is not null && await policy.CanPreviewAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The cookie's UI culture (the switch writes both halves the same), or null when there is none.</summary>
    private static string? RequestedCulture(HttpContext httpContext)
    {
        string? cookie = httpContext.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName];

        if (string.IsNullOrEmpty(cookie) || CookieRequestCultureProvider.ParseCookieValue(cookie) is not { } parsed)
        {
            return null;
        }

        StringSegment culture = parsed.UICultures.FirstOrDefault(segment => !StringSegment.IsNullOrEmpty(segment));

        return StringSegment.IsNullOrEmpty(culture) ? null : culture.Value;
    }
}
