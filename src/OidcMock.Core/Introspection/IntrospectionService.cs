using OidcMock.Core.Errors;
using OidcMock.Core.Grants;
using OidcMock.Core.Revocation;
using OidcMock.Core.Tokens;

namespace OidcMock.Core.Introspection;

/// <summary>
/// Introspecciona access tokens y refresh tokens. Un token desconocido, caducado, revocado o de otro
/// cliente se responde como inactive en lugar de con un error, tal como pide RFC 7662 seccion 2.2.
/// </summary>
public sealed class IntrospectionService(
    IAccessTokenReader accessTokenReader,
    IRefreshTokenStore refreshTokenStore,
    TimeProvider timeProvider,
    ITokenRevocationStore revocations) : IIntrospectionService
{
    public Result<IntrospectionResponse> Introspect(
        string token,
        string issuer,
        IEnumerable<string> audiences,
        string requestingClientId)
    {
        var refreshToken = FindRefreshTokenResponse(token, requestingClientId);
        if (refreshToken is not null)
        {
            return Result<IntrospectionResponse>.Ok(refreshToken);
        }

        var accessToken = FindAccessTokenResponse(token, issuer, [.. audiences], requestingClientId);

        return Result<IntrospectionResponse>.Ok(accessToken ?? Inactive());
    }

    private IntrospectionResponse? FindRefreshTokenResponse(string token, string requestingClientId)
    {
        var active = refreshTokenStore.List()
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Token, token, StringComparison.Ordinal) &&
                string.Equals(candidate.ClientId, requestingClientId, StringComparison.Ordinal));

        return active is null
            ? null
            : new IntrospectionResponse
            {
                Active = IsUsable(active),
                ClientId = active.ClientId,
                Subject = active.Subject,
                Scope = string.Join(' ', active.Scopes),
                TokenType = RefreshTokenType,
                ExpiresAt = active.ExpiresAt.ToUnixTimeSeconds(),
                IssuedAt = active.IssuedAt.ToUnixTimeSeconds()
            };
    }

    private IntrospectionResponse? FindAccessTokenResponse(
        string token,
        string issuer,
        List<string> audiences,
        string requestingClientId)
    {
        var read = accessTokenReader.Read(token, issuer, audiences);
        if (read.Failed)
        {
            return null;
        }

        var claims = read.Value!;
        if (!string.Equals(claims.ClientId, requestingClientId, StringComparison.Ordinal) ||
            revocations.IsAccessTokenRevoked(claims.TokenId))
        {
            return null;
        }

        return new IntrospectionResponse
        {
            Active = true,
            ClientId = claims.ClientId,
            Subject = claims.Subject,
            Scope = string.Join(' ', claims.Scopes),
            TokenType = AccessTokenType,
            ExpiresAt = claims.ExpiresAt.ToUnixTimeSeconds()
        };
    }

    /// <summary>
    /// Un refresh token solo sigue activo si no ha caducado y su familia no ha sido revocada: la
    /// revocacion en cascada es lo que impide renovar una sesion que el cliente ya cerro.
    /// </summary>
    private bool IsUsable(RefreshToken refreshToken) =>
        !refreshToken.IsExpiredAt(timeProvider.GetUtcNow()) &&
        !revocations.IsRefreshTokenFamilyRevoked(refreshToken.FamilyId);

    private static IntrospectionResponse Inactive() => new() { Active = false };

    private const string AccessTokenType = "Bearer";
    private const string RefreshTokenType = "refresh_token";
}