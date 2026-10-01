using System.Text.Json;
using System.Text.Json.Serialization;

namespace OidcMock.Core.Grants;

/// <summary>
/// Respuesta del token endpoint tal como la consume un cliente OAuth. Los nombres siguen el
/// formato de snake_case de RFC 6749 y OpenID Connect.
/// </summary>
public sealed record TokenResponse
{
    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = TokenTypes.Bearer;

    [JsonPropertyName("expires_in")]
    public required long ExpiresIn { get; init; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("id_token")]
    public string? IdToken { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }
}

/// <summary>
/// Cuerpo de error del token endpoint (RFC 6749 5.2), que siempre es JSON con estos dos campos.
/// </summary>
public sealed record TokenErrorResponse
{
    [JsonPropertyName("error")]
    public required string Error { get; init; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; init; }
}

/// <summary>
/// Tipo de token que se anuncia en token_type.
/// </summary>
public static class TokenTypes
{
    public const string Bearer = "Bearer";
}