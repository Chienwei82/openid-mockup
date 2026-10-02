using OidcMock.Core.Errors;
using OidcMock.Core.Tokens;
using OidcMock.Core.Users;

namespace OidcMock.Core.Grants;

/// <summary>
/// Grant password (RFC 6749 4.3): el cliente envía directamente usuario y contrasena. Esta obsoleto
/// en OAuth 2.1, pero el servidor real lo anuncia y hay aplicaciones que lo siguen usando.
/// </summary>
public sealed class PasswordGrantHandler(
    IUserStore userStore,
    IRefreshTokenStore refreshTokenStore,
    ITokenFactory tokenFactory,
    TimeProvider timeProvider) : IGrantHandler
{
    public string GrantType => GrantTypes.Password;

    public bool IssuesIdToken => true;

    public Task<Result<TokenResponse>> HandleAsync(TokenRequest request) =>
        Task.FromResult(Handle(request));

    private Result<TokenResponse> Handle(TokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.UserName) || string.IsNullOrEmpty(request.Password))
        {
            return Result<TokenResponse>.Fail(
                ProtocolErrors.InvalidRequest("El grant password requiere username y password."));
        }

        var user = userStore.FindByUserName(request.UserName);
        if (user is null || !PasswordsMatch(user, request.Password))
        {
            return Result<TokenResponse>.Fail(
                ProtocolErrors.InvalidGrant("El usuario o la contrasena no son correctos."));
        }

        return TokenResponseFactory.Issue(
            tokenFactory,
            refreshTokenStore,
            timeProvider,
            request.Issuer,
            request.Client,
            user,
            request.Scopes,
            timeProvider.GetUtcNow(),
            null,
            null,
            // El id_token solo existe con el scope openid (OpenID Connect Core 3.1.3.6): un canje con
            // scope=email llega aqui sin el, y devolver una identidad seria inventarla.
            includeIdToken: IssuesIdToken && IdTokenRules.GrantsIdToken(request.Scopes),
            includeRefreshToken: true);
    }

    private static bool PasswordsMatch(User user, string password) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(user.Password),
            System.Text.Encoding.UTF8.GetBytes(password));
}