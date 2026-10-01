using OidcMock.Core.Errors;

namespace OidcMock.Core.Tokens;

/// <summary>
/// Claims que el mock necesita leer de un access token ya emitido. Se validan con la misma clave que
/// publica el JWKS, de modo que un token firmado por otro se rechaza en vez de deserializarse.
/// </summary>
public sealed record AccessTokenClaims(
    string Subject,
    string ClientId,
    IReadOnlyList<string> Scopes,
    string TokenId,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Lee y valida un access token emitido por el mock.
/// </summary>
public interface IAccessTokenReader
{
    Result<AccessTokenClaims> Read(string accessToken, string issuer, IEnumerable<string> audiences);
}