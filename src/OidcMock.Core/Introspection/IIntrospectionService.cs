using System.Text.Json;
using System.Text.Json.Serialization;
using OidcMock.Core.Errors;
using OidcMock.Core.Grants;
using OidcMock.Core.Tokens;

namespace OidcMock.Core.Introspection;

/// <summary>
/// Respuesta de /connect/introspect (RFC 7662). Quando el token no existe o es invalido se responde
/// active=false, nunca un error: el endpoint no filtra informacion sobre por que fallo.
/// </summary>
public sealed record IntrospectionResponse
{
    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [JsonPropertyName("active")]
    public required bool Active { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    [JsonPropertyName("client_id")]
    public string? ClientId { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }

    [JsonPropertyName("exp")]
    public long? ExpiresAt { get; init; }

    [JsonPropertyName("iat")]
    public long? IssuedAt { get; init; }

    [JsonPropertyName("sub")]
    public string? Subject { get; init; }
}

/// <summary>
/// Describe el estado de un token para el endpoint de introspeccion. Solo el cliente propietario
/// puede consultar sus propios tokens.
/// </summary>
public interface IIntrospectionService
{
    Result<IntrospectionResponse> Introspect(
        string token,
        string issuer,
        IEnumerable<string> audiences,
        string requestingClientId);
}