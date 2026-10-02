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

public sealed class UserInfoIntrospectionRevocationTests : GrantHandlerTestBase
{
    private const string ClientId = "web-app-spa";

    private readonly IAccessTokenReader _reader;
    private readonly UserInfoService _userInfo;
    private readonly IntrospectionService _introspection;
    private readonly TokenRevocationService _revocation;

    public UserInfoIntrospectionRevocationTests()
    {
        _reader = new AccessTokenReader(SigningKeyProvider, Clock);
        _userInfo = new UserInfoService(
            _reader,
            new ProjectedUserInfoClaimsSource(new Core.Claims.ScopesClaimsProjector(ScopeStoreFixture.Create())),
            UserStore);
        _introspection = new IntrospectionService(_reader, RefreshTokens, Clock);
        _revocation = new TokenRevocationService(_reader, RefreshTokens);
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
    public void IntrospeccionDeUnRefreshTokenInformaSuVigencia()
    {
        var refreshToken = RefreshTokens.Issue(new RefreshTokenRequest(
            ClientId, "user-1", ["openid", "email"], TimeSpan.FromHours(1)));

        var response = Introspect(refreshToken.Token);

        Assert.True(response.Value?.Active);
        Assert.Equal("refresh_token", response.Value?.TokenType);
        Assert.Equal("openid email", response.Value?.Scope);
    }

    [Fact]
    public void IntrospeccionNoRevelaTokensDeOtroCliente()
    {
        var refreshToken = RefreshTokens.Issue(new RefreshTokenRequest(
            "backend-service", "user-1", ["openid"], TimeSpan.FromHours(1)));

        var response = Introspect(refreshToken.Token, requestingClientId: ClientId);

        Assert.False(response.Value?.Active);
    }

    [Fact]
    public void RevocarUnRefreshTokenLoDejaInactivo()
    {
        var refreshToken = RefreshTokens.Issue(new RefreshTokenRequest(
            ClientId, "user-1", ["openid"], TimeSpan.FromHours(1)));

        var revoked = Revoke(refreshToken.Token);
        var introspected = Introspect(refreshToken.Token);

        Assert.True(revoked.Succeeded);
        Assert.True(revoked.Value);
        Assert.False(introspected.Value?.Active);
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
        var refreshToken = RefreshTokens.Issue(new RefreshTokenRequest(
            ClientId, "user-1", ["openid"], TimeSpan.FromHours(1)));

        Revoke(refreshToken.Token);

        Assert.Empty(RefreshTokens.List());
    }

    private Result<IReadOnlyDictionary<string, System.Text.Json.JsonElement>> DescribeUserInfo(string token) =>
        _userInfo.Describe(token, Issuer, [ClientId]);

    private Result<IntrospectionResponse> Introspect(string token, string requestingClientId = ClientId) =>
        _introspection.Introspect(token, Issuer, [ClientId], requestingClientId);

    private Result<bool> Revoke(string token) => _revocation.Revoke(token, Issuer, [ClientId], ClientId);

    private string IssueAccessToken(IReadOnlyList<string>? scopes = null) =>
        Tokens.CreateAccessToken(new AccessTokenRequest(
            Issuer,
            ClientId,
            scopes ?? ["openid"],
            [ClientId],
            "user-1",
            SampleUser(),
            TimeSpan.FromMinutes(30)));
}
