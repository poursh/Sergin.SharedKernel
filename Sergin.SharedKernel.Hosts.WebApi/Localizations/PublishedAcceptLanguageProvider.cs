using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Hosts.WebApi.Localizations;

/// <summary>
/// The built-in <see cref="AcceptLanguageHeaderRequestCultureProvider"/>, narrowed to the first header culture (in
/// quality order) that <paramref name="accepted"/> holds (the default culture or a published language), with that
/// culture loaded into the cache before the request runs, since lookups are synchronous. A store that cannot be read
/// is logged as a warning and the request gets the default culture rather than failing.
/// </summary>
internal sealed class PublishedAcceptLanguageProvider(ITranslationCache cache, IList<CultureInfo> accepted) : RequestCultureProvider
{
    private readonly AcceptLanguageHeaderRequestCultureProvider header = new();

    public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        try
        {
            return await DetermineAsync(httpContext, httpContext.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            httpContext.RequestServices.GetService<ILogger<PublishedAcceptLanguageProvider>>()?.LogWarning(
                exception, "The translation store could not be read; the request gets the default culture.");
            return null;
        }
    }

    private async Task<ProviderCultureResult?> DetermineAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        // The accepted list starts with the default culture. Loading it also loads the language list the first time.
        await cache.EnsureLoadedAsync(accepted[0].Name, cancellationToken).ConfigureAwait(false);

        if (await header.DetermineProviderCultureResult(httpContext).ConfigureAwait(false) is not { } requested)
        {
            return null;
        }

        foreach (StringSegment name in requested.UICultures)
        {
            CultureInfo? culture = accepted.FirstOrDefault(
                candidate => name.Equals(candidate.Name, StringComparison.OrdinalIgnoreCase));

            if (culture is not null)
            {
                await cache.EnsureLoadedAsync(culture.Name, cancellationToken).ConfigureAwait(false);
                return new ProviderCultureResult(culture.Name);
            }
        }

        return null;
    }
}
