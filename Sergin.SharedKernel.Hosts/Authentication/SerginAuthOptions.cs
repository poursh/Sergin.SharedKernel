namespace Sergin.SharedKernel.Hosts.Authentication;

/// <summary>How a host establishes who the caller is.</summary>
public enum SerginAuthMode
{
    /// <summary>
    /// No authentication: every request runs as the single user configured under <c>Sergin:DevUser</c>.
    /// Permitted in the Development environment only — a host configured this way refuses to start
    /// anywhere else.
    /// </summary>
    DevUser = 0,

    /// <summary>
    /// OpenID Connect against Keycloak. Authentication comes from the provider; authorization still comes
    /// from Sergin's own store through <see cref="Application.Securities.Users.IExternalIdentityResolver"/>.
    /// </summary>
    Keycloak = 1,
}

/// <summary>Binds <c>Sergin:Auth</c>.</summary>
public sealed class SerginAuthOptions
{
    public const string SectionName = "Auth";

    public SerginAuthMode Mode { get; set; } = SerginAuthMode.DevUser;

    /// <summary>
    /// The realm's public issuer URL, exactly as the browser reaches it — for example
    /// <c>http://localhost:8080/realms/sergin</c>. It must match the <c>iss</c> in the tokens, so it is
    /// the browser-facing URL even when the server itself reaches Keycloak somewhere else.
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Where the server fetches the discovery document, when that differs from <see cref="Authority"/>.
    /// In Docker Compose the browser hits <c>localhost:8080</c> while the app container hits the service
    /// name, and only the metadata fetch can use the internal address — the issuer must stay public.
    /// Leave empty to derive it from <see cref="Authority"/>.
    /// </summary>
    public string MetadataAddress { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Audience a bearer token must carry to be accepted by an API host. Defaults to
    /// <see cref="ClientId"/> when left empty.
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Set false only for a local HTTP Keycloak. Leaving it true against <c>http://</c> makes metadata
    /// retrieval fail with an error that does not mention the scheme.
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    // string[] rather than IReadOnlyList<string>: the configuration binder supports arrays universally.
    public string[] Scopes { get; set; } = ["openid", "profile", "email"];

    public bool Validate(out string failure)
    {
        if (Mode != SerginAuthMode.Keycloak)
        {
            failure = string.Empty;
            return true;
        }

        if (!IsAbsoluteUrl(Authority))
        {
            failure = $"Sergin:{SectionName}:Authority must be an absolute URL when Mode is "
                + $"{nameof(SerginAuthMode.Keycloak)}, e.g. 'http://localhost:8080/realms/sergin'.";
            return false;
        }

        if (MetadataAddress.Length > 0 && !IsAbsoluteUrl(MetadataAddress))
        {
            failure = $"Sergin:{SectionName}:MetadataAddress must be an absolute URL when set, "
                + "e.g. 'http://sergin.identity:8080/realms/sergin/.well-known/openid-configuration'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ClientId))
        {
            failure = $"Sergin:{SectionName}:ClientId is required when Mode is {nameof(SerginAuthMode.Keycloak)}.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ClientSecret))
        {
            failure = $"Sergin:{SectionName}:ClientSecret is required when Mode is "
                + $"{nameof(SerginAuthMode.Keycloak)}. The Sergin client is confidential, not public.";
            return false;
        }

        if (Scopes.Length == 0)
        {
            failure = $"Sergin:{SectionName}:Scopes must contain at least 'openid'.";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    /// <summary>The audience an API host validates: <see cref="Audience"/>, falling back to the client id.</summary>
    public string ResolveAudience() => string.IsNullOrWhiteSpace(Audience) ? ClientId : Audience;

    private static bool IsAbsoluteUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
