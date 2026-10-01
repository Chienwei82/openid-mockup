using OidcMock.Core.Errors;
using OidcMock.Core.Grants;
using OidcMock.Core.Tokens;

namespace OidcMock.Core.Revocation;

/// <summary>
/// Revoca refresh tokens (los access tokens son sin estado y caducan solos, como en el servidor real).
/// Un token que no existe o no pertenece al cliente se responde con exito, igual que RFC 7009 pide.
/// </summary>
public sealed class TokenRevocationService(
    IAccessTokenReader accessTokenReader,
    IRefreshTokenStore refreshTokenStore) : ITokenRevocationService
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

    private bool RevokeRefreshToken(string token, string requestingClientId)
    {
        var owned = refreshTokenStore.List().Any(candidate =>
            string.Equals(candidate.Token, token, StringComparison.Ordinal) &&
            string.Equals(candidate.ClientId, requestingClientId, StringComparison.Ordinal));

        if (owned)
        {
            refreshTokenStore.Revoke(token);
        }

        return owned;
    }

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

        // El access token es un JWT sin estado: no hay nada que borrar. Se considera revocado para
        // que la respuesta sea coherente, y caduca por exp como haria el servidor real.
        return string.Equals(read.Value!.ClientId, requestingClientId, StringComparison.Ordinal);
    }
}