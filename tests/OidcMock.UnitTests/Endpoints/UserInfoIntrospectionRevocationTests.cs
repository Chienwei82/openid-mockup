using OidcMock.Core.Crypto;
using OidcMock.Core.Errors;
using OidcMock.Core.Grants;
using OidcMock.Core.Introspection;
using OidcMock.Core.Revocation;
using OidcMock.Core.Tokens;
using OidcMock.Core.UserInfo;
using OidcMock.UnitTests.Fixtures;
using OidcMock.UnitTests.Grants;

namespace OidcMock.UnitTests.Endpoints;

/// <summary>
/// Los tres endpoints que consumen un token ya emitido (userinfo, introspect y revocation) sobre el
/// mismo store de revocaciones: lo que uno revoca, los demas lo ven.
/// </summary>
public sealed class UserInfoIntrospectionRevocationTests : GrantHandlerTestBase
{
    private const string ClientId = "web-app-spa";
    private const string OtherClientId = "backend-service";

    private readonly UserInfoService _userInfo;
    private readonly IntrospectionService _introspection;
    private readonly TokenRevocationService _revocation;

    public UserInfoIntrospectionRevocationTests()
    {
        _userInfo = new UserInfoService(
            new AccessTokenReader(new SignedTokenValidator(SigningKeyProvider, Clock)),
            new ProjectedUserInfoClaimsSource(new Core.Claims.ScopesClaimsProjector(ScopeStoreFixture.Create())),
            UserStore,
            Revocations);
        _introspection = new IntrospectionService(
            new AccessTokenReader(new SignedTokenValidator(SigningKeyProvider, Clock)),
            RefreshTokens,
            Clock,
            Revocations);
        _revocation = new TokenRevocationService(
            new AccessTokenReader(new SignedTokenValidator(SigningKeyProvider, Clock)),
            RefreshTokens,
            Revocations);
    }

    [Fact]
    public void UserInfoDevuelveLosClaimsAutorizadosPorElToken()
    {
        var claims = DescribeUserInfo(IssueAccessToken(scopes: ["openid", "email"]));

        Assert.True(claims.Succeeded, claims.Error?.ToString());
        Assert.Equal("jperez@example.cr", claims.Value?["email"].GetString());
    }

    [Fact]
    public void UserInfoNoDevuelveClaimsFueraDeLosScopesDelToken()
    {
        var claims = DescribeUserInfo(IssueAccessToken(scopes: ["openid", "email"]));

        Assert.DoesNotContain("full_name", claims.Value!.Keys);
    }

    [Fact]
    public void UserInfoRechazaUnTokenInvalidoConInvalidToken()
    {
        var claims = DescribeUserInfo("no-es-un-token");

        Assert.Equal("invalid_token", claims.Error?.Code);
    }

    [Fact]
    public void UserInfoRechazaUnTokenCaducadoConInvalidToken()
    {
        var token = IssueAccessToken();
        Clock.Advance(TimeSpan.FromMinutes(31));

        Assert.Equal("invalid_token", DescribeUserInfo(token).Error?.Code);
    }

    /// <summary>
    /// El access token es un JWT sin estado: sin el registro de revocaciones, seguiria sirviendo en
    /// /userinfo hasta que caducara por si solo.
    /// </summary>
    [Fact]
    public void UserInfoRechazaUnAccessTokenRevocadoConInvalidToken()
    {
        var token = IssueAccessToken();
        Revoke(token);

        Assert.Equal("invalid_token", DescribeUserInfo(token).Error?.Code);
    }

    [Fact]
    public void IntrospeccionDeUnAccessTokenValidoInformaQueEstaActivo()
    {
        var response = Introspect(IssueAccessToken());

        Assert.True(response.Succeeded);
        Assert.True(response.Value?.Active);
        Assert.Equal(ClientId, response.Value?.ClientId);
    }

    [Fact]
    public void IntrospeccionDeUnTokenDesconocidoRespondeInactiveYNoError()
    {
        var response = Introspect("token-inventado");

        Assert.True(response.Succeeded);
        Assert.False(response.Value?.Active);
    }

    [Fact]
    public void IntrospeccionDeUnAccessTokenRevocadoRespondeInactive()
    {
        var token = IssueAccessToken();
        Revoke(token);

        Assert.False(Introspect(token).Value?.Active);
    }

    [Fact]
    public void IntrospeccionDeUnAccessTokenCaducadoRespondeInactive()
    {
        var token = IssueAccessToken();
        Clock.Advance(TimeSpan.FromMinutes(31));

        Assert.False(Introspect(token).Value?.Active);
    }

    /// <summary>
    /// /introspect no revela tokens de otro cliente: el token es valido, pero no es de quien pregunta,
    /// asi que la unica respuesta honesta es "no se nada de ese token" (active=false).
    /// </summary>
    [Fact]
    public void IntrospeccionDeUnAccessTokenDeOtroClienteRespondeInactive()
    {
        var token = IssueAccessToken(clientId: OtherClientId);

        Assert.False(Introspect(token).Value?.Active);
    }

    [Fact]
    public void IntrospeccionDeUnRefreshTokenInformaSuVigencia()
    {
        var refreshToken = IssueRefreshToken();

        var response = Introspect(refreshToken.Token);

        Assert.True(response.Value?.Active);
        Assert.Equal("refresh_token", response.Value?.TokenType);
        Assert.Equal("openid email", response.Value?.Scope);
    }

    [Fact]
    public void IntrospeccionNoRevelaTokensDeOtroCliente()
    {
        var refreshToken = IssueRefreshToken(OtherClientId);

        var response = Introspect(refreshToken.Token, requestingClientId: ClientId);

        Assert.False(response.Value?.Active);
    }

    [Fact]
    public void IntrospeccionDeUnRefreshTokenCaducadoRespondeInactive()
    {
        var refreshToken = IssueRefreshToken();
        Clock.Advance(TimeSpan.FromHours(2));

        Assert.False(Introspect(refreshToken.Token).Value?.Active);
    }

    [Fact]
    public void RevocarUnRefreshTokenLoDejaInactivo()
    {
        var refreshToken = IssueRefreshToken();

        var revoked = Revoke(refreshToken.Token);
        var introspected = Introspect(refreshToken.Token);

        Assert.True(revoked.Succeeded);
        Assert.True(revoked.Value);
        Assert.False(introspected.Value?.Active);
    }

    [Fact]
    public void RevocarUnAccessTokenLoDejaInactivo()
    {
        var token = IssueAccessToken();

        var revoked = Revoke(token);

        Assert.True(revoked.Succeeded);
        Assert.True(revoked.Value);
        Assert.False(Introspect(token).Value?.Active);
    }

    /// <summary>
    /// RFC 7009: revocar un refresh token no deja vivo al siguiente de la rotacion, porque el canje
    /// emite un token nuevo **de la misma familia**. Sin la cascada, un cliente que rota y luego
    /// revoca seguiria pudiendo renovar su sesion.
    /// </summary>
    [Fact]
    public void RevocarUnRefreshTokenInvalidaSuFamilia()
    {
        var original = IssueRefreshToken();
        var rotado = RefreshTokens.Redeem(original.Token).Value!;
        var siguiente = RefreshTokens.Issue(new RefreshTokenRequest(
            ClientId, "user-1", ["openid", "email"], TimeSpan.FromHours(1), rotado.FamilyId));

        Revoke(siguiente.Token);

        Assert.True(Revocations.IsRefreshTokenFamilyRevoked(siguiente.FamilyId));
        Assert.False(Introspect(siguiente.Token).Value?.Active);
    }

    /// <summary>
    /// Solo el cliente propietario revoca: presentado el token de otro cliente, la respuesta es 200
    /// (RFC 7009 2.2, no se revela si existo) pero el token sigue sirviendo para quien es dueno.
    /// </summary>
    [Fact]
    public void RevocarElTokenDeOtroClienteNoLoRevoca()
    {
        var token = IssueAccessToken(clientId: OtherClientId);

        var revoked = Revoke(token, requestingClientId: ClientId);
        var introspected = Introspect(token, requestingClientId: OtherClientId);

        Assert.True(revoked.Succeeded);
        Assert.False(revoked.Value);
        Assert.True(introspected.Value?.Active);
    }

    /// <summary>
    /// El caso que de verdad pasa en un cliente real: rota, y despues revoca el token que ya canjeo.
    /// Como el canje lo consumio, sigue en la memoria de "rotados" y no en los vivos: si la revocacion
    /// solo mirara los vivos, la familia quedaria viva y el rotado seguiria renovando la sesion.
    /// </summary>
    [Fact]
    public void RevocarUnRefreshTokenYaCanjeadoTambienCortaLaFamilia()
    {
        var original = IssueRefreshToken();
        var canjeado = RefreshTokens.Redeem(original.Token).Value!;
        var siguiente = RefreshTokens.Issue(new RefreshTokenRequest(
            ClientId, "user-1", ["openid", "email"], TimeSpan.FromHours(1), canjeado.FamilyId));

        Revoke(original.Token);

        Assert.True(Revocations.IsRefreshTokenFamilyRevoked(siguiente.FamilyId));
        Assert.False(Introspect(siguiente.Token).Value?.Active);
    }

    [Fact]
    public void RevocarUnTokenDesconocidoRespondeExitoSinError()
    {
        var revoked = Revoke("token-que-nunca-existio");

        Assert.True(revoked.Succeeded);
        Assert.False(revoked.Value);
    }

    [Fact]
    public void RevocarElRefreshTokenLoEliminaDelStore()
    {
        var refreshToken = IssueRefreshToken();

        Revoke(refreshToken.Token);

        Assert.Empty(RefreshTokens.List());
    }

    private Result<IReadOnlyDictionary<string, System.Text.Json.JsonElement>> DescribeUserInfo(string token) =>
        _userInfo.Describe(token, Issuer, [ClientId]);

    private Result<IntrospectionResponse> Introspect(string token, string requestingClientId = ClientId) =>
        _introspection.Introspect(token, Issuer, [requestingClientId], requestingClientId);

    private Result<bool> Revoke(string token, string requestingClientId = ClientId) =>
        _revocation.Revoke(token, Issuer, [requestingClientId], requestingClientId);

    private RefreshToken IssueRefreshToken(string clientId = ClientId) =>
        RefreshTokens.Issue(new RefreshTokenRequest(
            clientId, "user-1", ["openid", "email"], TimeSpan.FromHours(1)));

    private string IssueAccessToken(IReadOnlyList<string>? scopes = null, string clientId = ClientId) =>
        Tokens.CreateAccessToken(new AccessTokenRequest(
            Issuer,
            clientId,
            scopes ?? ["openid"],
            [clientId],
            "user-1",
            SampleUser(),
            TimeSpan.FromMinutes(30)));
}
