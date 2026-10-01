using OidcMock.Core.Codes;
using OidcMock.Core.Errors;
using OidcMock.Core.Tokens;
using OidcMock.Core.Users;

namespace OidcMock.Core.Grants;

/// <summary>
/// Grant authorization_code (RFC 6749 4.1): canjea el codigo por tokens, verificando el redirect_uri
/// y el code_verifier de PKCE contra lo que quedo registrado al emitir el codigo.
/// </summary>
public sealed class AuthorizationCodeGrantHandler(
    ICodeStore codeStore,
    IRefreshTokenStore refreshTokenStore,
    IUserStore userStore,
    ITokenFactory tokenFactory,
    TimeProvider timeProvider) : IGrantHandler
{
    private const string InvalidCode =
        "El codigo de autorizacion es invalido, ya fue usado, caduco o no corresponde a esta peticion.";

    public string GrantType => GrantTypes.AuthorizationCode;

    public bool IssuesIdToken => true;

    public Result<TokenResponse> Handle(TokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var redeemed = codeStore.Redeem(request.Code ?? string.Empty);
        if (redeemed.Failed)
        {
            return Result<TokenResponse>.Fail(redeemed.Error!);
        }

        var code = redeemed.Value!;
        var user = userStore.FindByUserName(code.UserName);

        if (user is null)
        {
            return Result<TokenResponse>.Fail(ProtocolErrors.InvalidGrant(InvalidCode));
        }

        if (!MatchesClientAndRedirectUri(code, request))
        {
            return Result<TokenResponse>.Fail(ProtocolErrors.InvalidGrant(InvalidCode));
        }

        if (!MatchesCodeVerifier(code, request))
        {
            return Result<TokenResponse>.Fail(ProtocolErrors.InvalidGrant(InvalidCode));
        }

        return TokenResponseFactory.Issue(
            tokenFactory,
            refreshTokenStore,
            timeProvider,
            request.Issuer,
            request.Client,
            user,
            code.Scopes,
            code.AuthenticatedAt,
            code.Nonce,
            null,
            includeIdToken: IssuesIdToken,
            includeRefreshToken: true);
    }

    private static bool MatchesClientAndRedirectUri(AuthorizationCode code, TokenRequest request) =>
        string.Equals(code.ClientId, request.Client.ClientId, StringComparison.Ordinal) &&
        string.Equals(code.RedirectUri ?? string.Empty, request.RedirectUri ?? string.Empty, StringComparison.Ordinal);

    private static bool MatchesCodeVerifier(AuthorizationCode code, TokenRequest request) =>
        !code.RequiresPkce ||
        CodeChallenges.Matches(code.CodeChallenge!, code.CodeChallengeMethod!, request.CodeVerifier ?? string.Empty);
}