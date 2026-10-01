using System.Text.Json.Serialization;

namespace OidcMock.Host.Stores.Configuration;

/// <summary>
/// Forma del archivo clients.json.
/// </summary>
internal sealed class ClientFile
{
    [JsonPropertyName("clients")]
    public List<ClientEntry>? Clients { get; init; }
}

internal sealed class ClientEntry
{
    [JsonPropertyName("client_id")]
    public string? ClientId { get; init; }

    [JsonPropertyName("client_secret")]
    public string? ClientSecret { get; init; }

    [JsonPropertyName("redirect_uris")]
    public List<string>? RedirectUris { get; init; }

    [JsonPropertyName("post_logout_redirect_uris")]
    public List<string>? PostLogoutRedirectUris { get; init; }

    [JsonPropertyName("allowed_grant_types")]
    public List<string>? AllowedGrantTypes { get; init; }

    [JsonPropertyName("allowed_scopes")]
    public List<string>? AllowedScopes { get; init; }

    [JsonPropertyName("require_pkce")]
    public bool RequirePkce { get; init; }

    [JsonPropertyName("require_client_secret")]
    public bool RequireClientSecret { get; init; }

    [JsonPropertyName("token_lifetimes")]
    public TokenLifetimesEntry? TokenLifetimes { get; init; }

    [JsonPropertyName("branding")]
    public BrandingEntry? Branding { get; init; }
}

internal sealed class TokenLifetimesEntry
{
    [JsonPropertyName("access_token")]
    public TimeSpan AccessToken { get; init; }

    [JsonPropertyName("id_token")]
    public TimeSpan IdentityToken { get; init; }

    [JsonPropertyName("refresh_token")]
    public TimeSpan RefreshToken { get; init; }

    [JsonPropertyName("authorization_code")]
    public TimeSpan AuthorizationCode { get; init; }
}

internal sealed class BrandingEntry
{
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("logo_url")]
    public string? LogoUrl { get; init; }

    [JsonPropertyName("primary_color")]
    public string? PrimaryColor { get; init; }
}
