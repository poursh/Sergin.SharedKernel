using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Sergin.SharedKernel.Application.Securities.Users;

namespace Sergin.SharedKernel.Hosts.Authentication;

/// <summary>
/// Renews the auth cookie from the refresh token when the access token is close to expiring, so a
/// Blazor circuit that stays open for hours never bounces the user back to the login page.
/// </summary>
/// <remarks>
/// Adapted from the ASP.NET Core <c>BlazorWebAppOidc</c> sample's refresher, with one Sergin-specific
/// change: the sample replaces the principal wholesale with the identity rebuilt from the fresh
/// <c>id_token</c>, which would discard the <see cref="SerginClaimTypes"/> claims stamped at sign-in
/// and silently strip every permission mid-session. This version carries those claims across.
/// </remarks>
internal sealed class CookieOidcRefresher(IOptionsMonitor<OpenIdConnectOptions> oidcOptionsMonitor)
{
    // A refresh response carries no nonce; the sign-in flow already validated one.
    private readonly OpenIdConnectProtocolValidator oidcTokenValidator = new() { RequireNonce = false };

    public async Task ValidateOrRefreshCookieAsync(CookieValidatePrincipalContext validateContext, string oidcScheme)
    {
        string? expirationText = validateContext.Properties.GetTokenValue("expires_at");

        if (!DateTimeOffset.TryParse(
            expirationText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset expiresAt))
        {
            return;
        }

        OpenIdConnectOptions oidcOptions = oidcOptionsMonitor.Get(oidcScheme);
        DateTimeOffset now = oidcOptions.TimeProvider?.GetUtcNow() ?? DateTimeOffset.UtcNow;

        if (now + TimeSpan.FromMinutes(5) < expiresAt)
        {
            return;
        }

        if (oidcOptions.ConfigurationManager is null)
        {
            return;
        }

        OpenIdConnectConfiguration configuration =
            await oidcOptions.ConfigurationManager.GetConfigurationAsync(validateContext.HttpContext.RequestAborted);

        if (string.IsNullOrEmpty(configuration.TokenEndpoint))
        {
            validateContext.RejectPrincipal();
            return;
        }

        using FormUrlEncodedContent request = new(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = oidcOptions.ClientId,
            ["client_secret"] = oidcOptions.ClientSecret,
            ["scope"] = string.Join(' ', oidcOptions.Scope),
            ["refresh_token"] = validateContext.Properties.GetTokenValue("refresh_token"),
        });

        using HttpResponseMessage response = await oidcOptions.Backchannel.PostAsync(
            new Uri(configuration.TokenEndpoint), request, validateContext.HttpContext.RequestAborted);

        if (!response.IsSuccessStatusCode)
        {
            validateContext.RejectPrincipal();
            return;
        }

        OpenIdConnectMessage message = new(
            await response.Content.ReadAsStringAsync(validateContext.HttpContext.RequestAborted));

        TokenValidationParameters validationParameters = oidcOptions.TokenValidationParameters.Clone();

        if (oidcOptions.ConfigurationManager is BaseConfigurationManager configurationManager)
        {
            validationParameters.ConfigurationManager = configurationManager;
        }
        else
        {
            validationParameters.ValidIssuer = configuration.Issuer;
            validationParameters.IssuerSigningKeys = configuration.SigningKeys;
        }

        TokenValidationResult validationResult =
            await oidcOptions.TokenHandler.ValidateTokenAsync(message.IdToken, validationParameters);

        if (!validationResult.IsValid || validationResult.ClaimsIdentity is null)
        {
            validateContext.RejectPrincipal();
            return;
        }

        if (validationResult.SecurityToken is JsonWebToken jsonWebToken)
        {
            JwtSecurityToken validatedIdToken = JwtSecurityTokenConverter.Convert(jsonWebToken);
            validatedIdToken.Payload["nonce"] = null;

            oidcTokenValidator.ValidateTokenResponse(new OpenIdConnectProtocolValidationContext
            {
                ProtocolMessage = message,
                ClientId = oidcOptions.ClientId,
                ValidatedIdToken = validatedIdToken,
            });
        }

        CarryOverSerginClaims(validateContext.Principal, validationResult.ClaimsIdentity);

        validateContext.ShouldRenew = true;
        validateContext.ReplacePrincipal(new ClaimsPrincipal(validationResult.ClaimsIdentity));

        DateTimeOffset renewedExpiresAt = now + TimeSpan.FromSeconds(
            int.Parse(message.ExpiresIn, NumberStyles.Integer, CultureInfo.InvariantCulture));

        validateContext.Properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = message.AccessToken },
            new AuthenticationToken { Name = "id_token", Value = message.IdToken },
            new AuthenticationToken { Name = "refresh_token", Value = message.RefreshToken },
            new AuthenticationToken { Name = "token_type", Value = message.TokenType },
            new AuthenticationToken
            {
                Name = "expires_at",
                Value = renewedExpiresAt.ToString("o", CultureInfo.InvariantCulture),
            },
        ]);
    }

    /// <summary>
    /// Moves the authorization Sergin resolved at sign-in onto the rebuilt identity. Without this the
    /// user keeps their name and loses every permission the moment their token refreshes.
    /// </summary>
    private static void CarryOverSerginClaims(ClaimsPrincipal? previous, ClaimsIdentity refreshed)
    {
        if (previous is null)
        {
            return;
        }

        foreach (Claim claim in previous.FindAll(IsSerginClaim))
        {
            refreshed.AddClaim(claim);
        }
    }

    private static bool IsSerginClaim(Claim claim)
        => string.Equals(claim.Type, SerginClaimTypes.UserId, StringComparison.Ordinal)
            || string.Equals(claim.Type, SerginClaimTypes.Permission, StringComparison.Ordinal);
}
