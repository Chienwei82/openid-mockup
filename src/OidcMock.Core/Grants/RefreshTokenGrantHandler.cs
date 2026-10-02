using OidcMock.Core.Errors;
using OidcMock.Core.Revocation;
using OidcMock.Core.Tokens;
using OidcMock.Core.Users;

namespace OidcMock.Core.Grants;

/// <summary>
/// Grant refresh_token (RFC 6749 6): canjea el refresh token por tokens nuevos. La rotacion es
/// obligatoria, cada canje emite un token nuevo de la misma familia y el canjeado queda inutilizable.
/// El scope puede solo reducirse (RFC 6749 6): pedir uno que no estaba en el token original es
/// invalid_scope. La reutilizacion de un token ya rotado revoca la familia completa, porque un token
/// robado que reaparece indica que alguien copio la cadena.
/// </summary>
public sealed class RefreshTokenGrantHandler(
    IRefreshTokenStore refreshTokenStore,
    IUserStore userStore,
    ITokenFactory tokenFactory,
    TimeProvider timeProvider,
    ITokenRevocationStore revocations) : IGrantHandler
{
    public string GrantType => GrantTypes.RefreshToken;

    public bool IssuesIdToken => false;

    public Task<Result<TokenResponse>> HandleAsync(TokenRequest request) =>
        Task.FromResult(Handle(request));

    private Result<TokenResponse> Handle(TokenRequest request)
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

        // Segunda puerta, ya con la familia conocida: aunque el token se hubiera colado en el store,
        // una familia revocada no se renueva. Asi el store de revocaciones manda sobre cualquier otro.
        if (revocations.IsRefreshTokenFamilyRevoked(refreshToken.FamilyId))
        {
            return Result<TokenResponse>.Fail(ProtocolErrors.InvalidGrant(
                "La familia del refresh token fue revocada."));
        }

        var scopes = NarrowScopes(refreshToken.Scopes, request);
        if (scopes is null)
        {
            return Result<TokenResponse>.Fail(ProtocolErrors.InvalidScope(
                "El refresh token no concede los scopes solicitados; solo admite reducirlos."));
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
            scopes,
            timeProvider.GetUtcNow(),
            null,
            null,
            includeIdToken: IssuesIdToken,
            includeRefreshToken: true,
            refreshTokenFamilyId: refreshToken.FamilyId);
    }

    /// <summary>
    /// Devuelve el scope efectivo del canje, o null si se pidio uno que el token original no concede.
    /// Pedir menos esta permitido; pedir mas es una ampliacion y se rechaza.
    /// </summary>
    private static IReadOnlyList<string>? NarrowScopes(
        IReadOnlyList<string> grantedScopes,
        TokenRequest request)
    {
        if (!request.ScopesRequested)
        {
            return grantedScopes;
        }

        return request.Scopes.All(grantedScopes.Contains) ? request.Scopes : null;
    }
}