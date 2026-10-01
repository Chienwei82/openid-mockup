using OidcMock.Core.Users;

namespace OidcMock.Core.Tokens;

/// <summary>
/// Emite los tokens firmados del mock (id_token y access_token) con la clave que publica el JWKS.
/// </summary>
public interface ITokenFactory
{
    string CreateIdToken(IdTokenRequest request);

    string CreateAccessToken(AccessTokenRequest request);
}