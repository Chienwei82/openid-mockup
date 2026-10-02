using OidcMock.Core.Errors;
using OidcMock.Core.Grants;
using OidcMock.Core.Tokens;

namespace OidcMock.Core.Revocation;

/// <summary>
/// Revoca access tokens y refresh tokens (RFC 7009). Un token que no existe o no pertenece al cliente
/// se responde con exito, igual que pide la especificacion, para no revelar si existio.
/// </summary>
public sealed class TokenRevocationService(
    IAccessTokenReader accessTokenReader,
    IRefreshTokenStore refreshTokenStore,
    ITokenRevocationStore revocations) : ITokenRevocationService
{
    public Result<bool> Revoke(
        string token,
        string issuer,
        IEnumerable<string> audiences,
        string requestingClientId)
    {
        if (string.IsNullOrEmpty(token))
        {
            return Result<bool>.Ok(false);
        }

        return Result<bool>.Ok(RevokeRefreshToken(token, requestingClientId) || RevokeAccessToken(token, issuer, audiences, requestingClientId));
    }

    /// <summary>
    /// Revocar un refresh token corta la familia entera: la rotacion emite un token nuevo de la misma
    /// familia, asi que revocar solo el presentado dejaria vivo al siguiente de la cadena. Se busca el
    /// token entre los vivos **y** entre los ya canjeados, porque el camino normal de un cliente es
    /// canjear y despues revocar el token anterior, que ya no esta vivo.
    /// </summary>
    private bool RevokeRefreshToken(string token, string requestingClientId)
    {
        var issued = refreshTokenStore.FindIssued(token);
        if (issued is null || !string.Equals(issued.ClientId, requestingClientId, StringComparison.Ordinal))
        {
            return false;
        }

        refreshTokenStore.RevokeFamily(issued.FamilyId);
        revocations.RevokeRefreshTokenFamily(issued.FamilyId, issued.ExpiresAt);

        return true;
    }

    /// <summary>
    /// El access token es un JWT sin estado: no hay nada que borrar en el token, asi que se registra
    /// su <c>jti</c> como revocado. A partir de ahi /userinfo responde 401 y /introspect active=false,
    /// en vez de seguir aceptando el token hasta que su <c>exp</c> llegue.
    /// </summary>
    private bool RevokeAccessToken(
        string token,
        string issuer,
        IEnumerable<string> audiences,
        string requestingClientId)
    {
        var read = accessTokenReader.Read(token, issuer, audiences);
        if (read.Failed)
        {
            return false;
        }

        var claims = read.Value!;
        if (!string.Equals(claims.ClientId, requestingClientId, StringComparison.Ordinal))
        {
            return false;
        }

        revocations.RevokeAccessToken(claims.TokenId, claims.ExpiresAt);

        return true;
    }
}