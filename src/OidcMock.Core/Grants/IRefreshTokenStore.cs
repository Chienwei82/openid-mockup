using OidcMock.Core.Codes;
using OidcMock.Core.Errors;

namespace OidcMock.Core.Grants;

/// <summary>
/// Almacena los refresh tokens emitidos, con expiracion y de un solo uso: cada canje rota el token.
/// </summary>
public interface IRefreshTokenStore
{
    RefreshToken Issue(RefreshTokenRequest request);

    /// <summary>
    /// Canjea el token y lo consume. Devuelve invalid_grant si no existe, ya se canjeo o caduco.
    /// </summary>
    Result<RefreshToken> Redeem(string token);

    void Revoke(string token);

    /// <summary>Descarta los refresh tokens caducados, para que no crezcan sin limite.</summary>
    void Expire();

    IReadOnlyList<RefreshToken> List();
}

/// <summary>
/// Datos con los que se emite un refresh token.
/// </summary>
public sealed record RefreshTokenRequest(
    string ClientId,
    string Subject,
    IReadOnlyList<string> Scopes,
    TimeSpan Lifetime);