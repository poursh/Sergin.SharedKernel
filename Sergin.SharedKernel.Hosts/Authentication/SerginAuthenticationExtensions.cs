using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Sergin.SharedKernel.Application.Securities;
using Sergin.SharedKernel.Application.Securities.Users;
using JwtTokenValidatedContext = Microsoft.AspNetCore.Authentication.JwtBearer.TokenValidatedContext;
using OidcTokenValidatedContext = Microsoft.AspNetCore.Authentication.OpenIdConnect.TokenValidatedContext;

namespace Sergin.SharedKernel.Hosts.Authentication;

/// <summary>
/// Wires the identity half of a Sergin host. Authorization is unaffected — permissions still come from
/// Sergin's own store through <see cref="IExternalIdentityResolver"/> and are still enforced by
/// <c>PermissionCheckPipelineBehavior</c>; this only decides who the caller is.
/// </summary>
public static class SerginAuthenticationExtensions
{
    /// <summary>The OpenID Connect scheme name. Named rather than defaulted so sign-out can target it.</summary>
    public const string OidcScheme = "SerginOidc";

    /// <summary>The route prefix owned by the login/logout endpoints.</summary>
    public const string AuthenticationPath = "/authentication";

    private const string SubjectClaimType = "sub";

    /// <summary>
    /// Binds and validates <c>Sergin:Auth</c>, then hands the bound value back so the caller can branch on
    /// <see cref="SerginAuthOptions.Mode"/> at registration time. Validation still runs at startup, so a
    /// misconfigured Keycloak host fails naming the offending key even though registration read the value first.
    /// </summary>
    public static SerginAuthOptions AddSerginAuthOptions(
        this IServiceCollection services, IConfigurationSection serginSection)
    {
        IConfigurationSection authSection = serginSection.GetSection(SerginAuthOptions.SectionName);

        services.AddOptions<SerginAuthOptions>()
            .Bind(authSection)
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<SerginAuthOptions>, SerginAuthOptionsValidator>();

        return authSection.Get<SerginAuthOptions>() ?? new SerginAuthOptions();
    }

    /// <summary>
    /// Cookie plus OpenID Connect, for a host a human signs into with a browser. The cookie is the session;
    /// the OIDC handler runs only at sign-in and refresh.
    /// </summary>
    public static IServiceCollection AddSerginKeycloakCookieOidc(
        this IServiceCollection services, SerginAuthOptions options)
    {
        services.AddSingleton<CookieOidcRefresher>();
        services.AddSerginClaimsUserContext();

        services.AddAuthentication(configure =>
        {
            configure.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            configure.DefaultChallengeScheme = OidcScheme;
        })
        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddOpenIdConnect(OidcScheme, oidcOptions => ConfigureOidc(oidcOptions, options));

        // The refresher needs the OIDC options, and the OIDC handler needs the cookie — configuring the
        // cookie's event here rather than inline above keeps that from becoming a circular resolution.
        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<CookieOidcRefresher>((cookieOptions, refresher) =>
                cookieOptions.Events.OnValidatePrincipal =
                    context => refresher.ValidateOrRefreshCookieAsync(context, OidcScheme));

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Bearer tokens, for an API host. Unlike the cookie flow there is nowhere to stamp permissions once,
    /// so they are resolved per token and cached briefly against the token's subject.
    /// </summary>
    public static IServiceCollection AddSerginKeycloakJwtBearer(
        this IServiceCollection services, SerginAuthOptions options)
    {
        services.AddMemoryCache();
        services.AddSerginClaimsUserContext();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwtOptions =>
            {
                jwtOptions.Authority = options.Authority;
                jwtOptions.Audience = options.ResolveAudience();
                jwtOptions.RequireHttpsMetadata = options.RequireHttpsMetadata;
                jwtOptions.MapInboundClaims = false;
                jwtOptions.TokenValidationParameters.NameClaimType = "preferred_username";

                if (options.MetadataAddress.Length > 0)
                {
                    jwtOptions.MetadataAddress = options.MetadataAddress;
                    jwtOptions.TokenValidationParameters.ValidIssuer = options.Authority;
                }

                jwtOptions.Events = new JwtBearerEvents { OnTokenValidated = StampCachedPermissionsAsync };
            });

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Fails fast when a host enables Keycloak but no module supplies the authorization half. Without this
    /// the first sign-in would be the thing that discovers it, and it would fail as a 500 mid-callback.
    /// </summary>
    public static void EnsureExternalIdentityResolverRegistered(IServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();

        if (scope.ServiceProvider.GetService<IExternalIdentityResolver>() is not null)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Sergin:{SerginAuthOptions.SectionName}:Mode is '{nameof(SerginAuthMode.Keycloak)}', but no module "
            + $"registered an {nameof(IExternalIdentityResolver)}. Keycloak authenticates the caller; a module "
            + "must still map that identity onto a Sergin user and its permissions.");
    }

    /// <summary>
    /// The sign-in and sign-out routes. Both are anonymous by necessity: requiring authorization to reach
    /// the login endpoint is a redirect loop, and requiring it to log out strands an expired session.
    /// </summary>
    public static IEndpointRouteBuilder MapSerginLoginAndLogout(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(AuthenticationPath).AllowAnonymous();

        group.MapGet("/login", (string? returnUrl) =>
            TypedResults.Challenge(PropertiesFor(returnUrl), [OidcScheme]));

        // POST, not GET: a GET sign-out is triggerable by any third-party page embedding the URL.
        // Antiforgery stays on — the sign-out form renders an <AntiforgeryToken /> to satisfy it.
        group.MapPost("/logout", ([FromForm] string? returnUrl) =>
            TypedResults.SignOut(
                PropertiesFor(returnUrl),
                [CookieAuthenticationDefaults.AuthenticationScheme, OidcScheme]));

        return endpoints;
    }

    /// <summary>
    /// The user context both Keycloak flows share: read from the request's authenticated principal,
    /// never from a database. Registered here rather than by each host so the two cannot drift.
    /// </summary>
    private static void AddSerginClaimsUserContext(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddTransient<IUserContextFactory, ClaimsPrincipalUserContextFactory>();
    }

    private static AuthenticationProperties PropertiesFor(string? returnUrl)
        => new() { RedirectUri = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl };

    private static void ConfigureOidc(OpenIdConnectOptions oidcOptions, SerginAuthOptions options)
    {
        oidcOptions.Authority = options.Authority;
        oidcOptions.ClientId = options.ClientId;
        oidcOptions.ClientSecret = options.ClientSecret;
        oidcOptions.RequireHttpsMetadata = options.RequireHttpsMetadata;
        oidcOptions.ResponseType = OpenIdConnectResponseType.Code;
        oidcOptions.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        oidcOptions.SaveTokens = true;
        oidcOptions.MapInboundClaims = false;
        oidcOptions.TokenValidationParameters.NameClaimType = "preferred_username";

        if (options.MetadataAddress.Length > 0)
        {
            // Compose splits these: the browser is redirected to the public Authority while the server
            // fetches metadata over the container network. The issuer must stay the one the browser saw.
            oidcOptions.MetadataAddress = options.MetadataAddress;
            oidcOptions.TokenValidationParameters.ValidIssuer = options.Authority;
        }

        oidcOptions.Scope.Clear();

        foreach (string scope in options.Scopes)
        {
            oidcOptions.Scope.Add(scope);
        }

        oidcOptions.Events.OnTokenValidated = StampResolvedPermissionsAsync;
    }

    /// <summary>
    /// Turns the provider's assertion into Sergin's own answer, once, at sign-in. Everything downstream
    /// reads the resulting claims, which is what keeps <c>IUserContext</c> free of database work.
    /// </summary>
    private static async Task StampResolvedPermissionsAsync(OidcTokenValidatedContext context)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        IExternalIdentityResolver resolver =
            context.HttpContext.RequestServices.GetRequiredService<IExternalIdentityResolver>();

        ExternalIdentityResult result = await resolver.ResolveAsync(
            ReadExternalIdentity(context.Principal), context.HttpContext.RequestAborted);

        Stamp(identity, result);
    }

    private static async Task StampCachedPermissionsAsync(JwtTokenValidatedContext context)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        ExternalIdentity external = ReadExternalIdentity(context.Principal);
        IMemoryCache cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();

        ExternalIdentityResult result = await cache.GetOrCreateAsync(
            $"sergin:identity:{external.Subject}",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

                IExternalIdentityResolver resolver =
                    context.HttpContext.RequestServices.GetRequiredService<IExternalIdentityResolver>();

                return await resolver.ResolveAsync(external, context.HttpContext.RequestAborted);
            }) ?? new ExternalIdentityResult(Guid.Empty, []);

        Stamp(identity, result);
    }

    private static void Stamp(ClaimsIdentity identity, ExternalIdentityResult result)
    {
        identity.AddClaim(new Claim(
            SerginClaimTypes.UserId, result.UserId.ToString("D", CultureInfo.InvariantCulture)));

        foreach (Permission permission in result.Permissions)
        {
            identity.AddClaim(new Claim(SerginClaimTypes.Permission, permission.Value));
        }
    }

    private static ExternalIdentity ReadExternalIdentity(ClaimsPrincipal principal)
    {
        string subject = FindValue(principal, SubjectClaimType)
            ?? FindValue(principal, ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException(
                "The identity provider returned a token with no 'sub' claim, so the caller cannot be linked "
                + "to a Sergin user.");

        return new ExternalIdentity(
            subject,
            Read(principal, "preferred_username", ClaimTypes.Name),
            Read(principal, "email", ClaimTypes.Email),
            Read(principal, "given_name", ClaimTypes.GivenName),
            Read(principal, "family_name", ClaimTypes.Surname));
    }

    private static string Read(ClaimsPrincipal principal, string primaryType, string fallbackType)
        => FindValue(principal, primaryType) ?? FindValue(principal, fallbackType) ?? string.Empty;

    private static string? FindValue(ClaimsPrincipal principal, string claimType)
        => principal.FindFirst(claimType)?.Value;
}
