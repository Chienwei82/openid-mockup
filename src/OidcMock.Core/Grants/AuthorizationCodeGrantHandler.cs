using OidcMock.Core.Clients;
using OidcMock.Core.Codes;
using OidcMock.Core.Errors;
using OidcMock.Core.Scopes;
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

    public Task<Result<TokenResponse>> HandleAsync(TokenRequest request) =>
        Task.FromResult(Handle(request));

    private Result<TokenResponse> Handle(TokenRequest request)
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
            includeIdToken: IssuesIdToken && GrantsIdToken(code.Scopes),
            includeRefreshToken: GrantsRefreshToken(request.Client, code.Scopes));
    }

    /// <summary>
    /// El id_token solo tiene sentido con el scope openid (OpenID Connect Core 3.1.3.6), que ademas
    /// el authorize endpoint exige siempre, asi que en la practica esta comprobacion es una red de
    /// seguridad para un code emitido por otra via.
    /// </summary>
    private static bool GrantsIdToken(IReadOnlyList<string> scopes) =>
        scopes.Contains(ScopeNames.OpenId, StringComparer.Ordinal);

    /// <summary>
    /// Un refresh token solo si el cliente lo pidio con offline_access y su configuracion se lo
    /// permite (OpenID Connect Core 11). Sin ese scope no se emite, aunque el grant lo soporte.
    /// </summary>
    private static bool GrantsRefreshToken(Client client, IReadOnlyList<string> scopes) =>
        scopes.Contains(ScopeNames.OfflineAccess, StringComparer.Ordinal) &&
        client.AllowsScope(ScopeNames.OfflineAccess);

    private static bool MatchesClientAndRedirectUri(AuthorizationCode code, TokenRequest request) =>
        string.Equals(code.ClientId, request.Client.ClientId, StringComparison.Ordinal) &&
        string.Equals(code.RedirectUri ?? string.Empty, request.RedirectUri ?? string.Empty, StringComparison.Ordinal);

    private static bool MatchesCodeVerifier(AuthorizationCode code, TokenRequest request) =>
        !code.RequiresPkce ||
        CodeChallenges.Matches(code.CodeChallenge!, code.CodeChallengeMethod!, request.CodeVerifier ?? string.Empty);
}