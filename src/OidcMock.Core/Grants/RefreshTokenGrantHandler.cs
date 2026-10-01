using OidcMock.Core.Errors;
using OidcMock.Core.Tokens;
using OidcMock.Core.Users;

namespace OidcMock.Core.Grants;

/// <summary>
/// Grant refresh_token (RFC 6749 6): canjea el refresh token por tokens nuevos. El token canjeado se
/// consume y se rota, de modo que un refresh token robado sirve para un unico uso.
/// </summary>
public sealed class RefreshTokenGrantHandler(
    IRefreshTokenStore refreshTokenStore,
    IUserStore userStore,
    ITokenFactory tokenFactory,
    TimeProvider timeProvider) : IGrantHandler
{
    public string GrantType => GrantTypes.RefreshToken;

    public bool IssuesIdToken => false;

    public Result<TokenResponse> Handle(TokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var redeemed = refreshTokenStore.Redeem(request.RefreshToken ?? string.Empty);
        if (redeemed.Failed)
        {
            return Result<TokenResponse>.Fail(redeemed.Error!);
        }

        var refreshToken = redeemed.Value!;
        if (!string.Equals(refreshToken.ClientId, request.Client.ClientId, StringComparison.Ordinal))
        {
            return Result<TokenResponse>.Fail(ProtocolErrors.InvalidGrant(
                "El refresh token no fue emitido para este cliente."));
        }

        var user = userStore.FindBySubject(refreshToken.Subject);
        if (user is null)
        {
            return Result<TokenResponse>.Fail(
                ProtocolErrors.InvalidGrant("El usuario del refresh token ya no existe en el mock."));
        }

        return TokenResponseFactory.Issue(
            tokenFactory,
            refreshTokenStore,
            timeProvider,
            request.Issuer,
            request.Client,
            user,
            refreshToken.Scopes,
            timeProvider.GetUtcNow(),
            null,
            null,
            includeIdToken: IssuesIdToken,
            includeRefreshToken: true);
    }
}