using System.Text.Json;
using System.Text.Json.Serialization;

namespace OidcMock.Core.Discovery;

/// <summary>
/// JWKS publicado por el mock: la clave publica RSA usada para firmar los tokens.
/// </summary>
public sealed record JsonWebKeySet
{
    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [JsonPropertyName("keys")]
    public required IReadOnlyList<JsonWebKey> Keys { get; init; }
}

/// <summary>
/// Clave publica dentro del JWKS.
/// </summary>
public sealed record JsonWebKey
{
    [JsonPropertyName("kty")]
    public required string KeyType { get; init; }

    [JsonPropertyName("use")]
    public required string Use { get; init; }

    [JsonPropertyName("kid")]
    public required string KeyId { get; init; }

    [JsonPropertyName("alg")]
    public required string Algorithm { get; init; }

    [JsonPropertyName("n")]
    public required string Modulus { get; init; }

    [JsonPropertyName("e")]
    public required string Exponent { get; init; }
}
