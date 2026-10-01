using OidcMock.Core.Clients;
using OidcMock.Core.Errors;
using OidcMock.Core.Scopes;
using OidcMock.Core.Tokens;
using OidcMock.Core.Users;

namespace OidcMock.Core.Grants;

/// <summary>
/// Emite el conjunto de tokens de una respuesta del token endpoint. Todos los grants que sirven
/// tokens a un usuario pasan por aqui, para que el access token, el id_token y el refresh token se
/// construyan igual en el authorization_code, el refresh_token y el password.
/// </summary>
public static class TokenResponseFactory
{
    public static Result<TokenResponse> Issue(
        ITokenFactory tokenFactory,
        IRefreshTokenStore refreshTokenStore,
        TimeProvider timeProvider,
        string issuer,
        Client client,
        User user,
        IReadOnlyList<string> scopes,
        DateTimeOffset authenticationTime,
        string? nonce,
        string? authorizationCode,
        bool includeRefreshToken)
    {
        ArgumentNullException.ThrowIfNull(tokenFactory);
        ArgumentNullException.ThrowIfNull(refreshTokenStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(scopes);

        var accessToken = tokenFactory.CreateAccessToken(new AccessTokenRequest(
            issuer,
            client.ClientId,
            scopes,
            [client.ClientId],
            user.Subject,
            user,
            timeProvider.GetUtcNow(),
            client.TokenLifetimes.AccessToken));

        var idToken = tokenFactory.CreateIdToken(new IdTokenRequest(
            issuer,
            client.ClientId,
            scopes,
            user,
            authenticationTime,
            client.TokenLifetimes.IdentityToken,
            nonce,
            accessToken,
            authorizationCode));

        var refreshToken = includeRefreshToken
            ? IssueRefreshToken(refreshTokenStore, client, user, scopes)
            : null;

        return Result<TokenResponse>.Ok(new TokenResponse
        {
            AccessToken = accessToken,
            ExpiresIn = (long)client.TokenLifetimes.AccessToken.TotalSeconds,
            IdToken = idToken,
            RefreshToken = refreshToken,
            Scope = string.Join(' ', scopes)
        });
    }

    private static string IssueRefreshToken(
        IRefreshTokenStore refreshTokenStore,
        Client client,
        User user,
        IReadOnlyList<string> scopes) =>
        refreshTokenStore.Issue(new RefreshTokenRequest(
            client.ClientId,
            user.Subject,
            scopes,
            client.TokenLifetimes.RefreshToken)).Token;
}