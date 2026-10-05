using OidcMock.Core.Codes;
using OidcMock.Core.Errors;
using OidcMock.Core.Grants;
using OidcMock.UnitTests.Fixtures;
using OidcMock.UnitTests.Tokens;

namespace OidcMock.UnitTests.Grants;

public sealed class AuthorizationCodeGrantHandlerTests : GrantHandlerTestBase
{
    private const string ClientId = "web-app-spa";

    private readonly AuthorizationCodeGrantHandler _handler;

    public AuthorizationCodeGrantHandlerTests() =>
        _handler = new AuthorizationCodeGrantHandler(CodeStore, RefreshTokens, UserStore, Tokens);

    [Fact]
    public async Task CanjeaUnCodigoValidoYEmiteTokens()
    {
        var response = await HandleAsync(IssueCode(CodeStore));

        Assert.True(response.Succeeded, response.Error?.ToString());
        Assert.Equal(TokenTypes.Bearer, response.Value?.TokenType);
        Assert.Equal((long)TimeSpan.FromMinutes(30).TotalSeconds, response.Value?.ExpiresIn);
    }

    [Fact]
    public async Task EmiteUnAccessTokenValidoParaElSubjectAutorizado()
    {
        var response = await HandleAsync(IssueCode(CodeStore));

        var result = Validator.Validate(response.Value!.AccessToken, Issuer, [ClientId], Clock);

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal("user-1", TokenTestValidator.ReadClaim(response.Value.AccessToken, "sub").GetString());
    }

    [Fact]
    public async Task EmiteUnIdTokenConElNonceDeLaPeticionOriginal()
    {
        var response = await HandleAsync(IssueCode(CodeStore, nonce: "n-abc"));

        Assert.Equal("n-abc", TokenTestValidator.ReadClaim(response.Value!.IdToken!, "nonce").GetString());
    }

    [Fact]
    public async Task EmiteUnRefreshTokenQueSePuedeCanjear()
    {
        var response = await HandleAsync(IssueCode(CodeStore, scopes: ["openid", "offline_access"]));

        Assert.NotNull(response.Value?.RefreshToken);

        var refreshed = RefreshTokens.Redeem(response.Value!.RefreshToken!);

        Assert.True(refreshed.Succeeded);
        Assert.Equal("user-1", refreshed.Value?.Subject);
    }

    [Fact]
    public async Task ElAccessTokenLevaLosScopesConcedidosEnElCodigo()
    {
        var response = await HandleAsync(IssueCode(CodeStore));

        Assert.Equal("openid email", response.Value?.Scope);
    }

    [Fact]
    public async Task RechazaUnCodigoDesconocido()
    {
        var response = await HandleAsync(codeValue: "codigo-inventado");

        Assert.Equal("invalid_grant", response.Error?.Code);
    }

    [Fact]
    public async Task RechazaUnCodigoYaCanjeado()
    {
        var code = IssueCode(CodeStore);
        await HandleAsync(code);

        Assert.Equal("invalid_grant", (await HandleAsync(code)).Error?.Code);
    }

    [Fact]
    public async Task RechazaUnRedirectUriQueNoCoincideConElDelCodigo()
    {
        var response = await HandleAsync(IssueCode(CodeStore), redirectUri: "https://localhost:5173/otro");

        Assert.Equal("invalid_grant", response.Error?.Code);
    }

    [Fact]
    public async Task ExigeElCodeVerifierCuandoElCodigoTieneCodeChallenge()
    {
        var response = await HandleAsync(IssueCode(CodeStore, withChallenge: true), codeVerifier: null);

        Assert.Equal("invalid_grant", response.Error?.Code);
    }

    [Fact]
    public async Task RechazaUnCodeVerifierQueNoCorrespondeAlChallenge()
    {
        var response = await HandleAsync(
            IssueCode(CodeStore, withChallenge: true),
            codeVerifier: "otro-verificador-que-no-corresponde-al-desafio-XXXXX");

        Assert.Equal("invalid_grant", response.Error?.Code);
    }

    [Fact]
    public async Task AceptaElCodeVerifierQueCorrespondeAlChallenge()
    {
        var response = await HandleAsync(IssueCode(CodeStore, withChallenge: true), codeVerifier: Verifier);

        Assert.True(response.Succeeded, response.Error?.ToString());
    }

    [Fact]
    public async Task UnCodigoSinPkceNoExigeCodeVerifier()
    {
        var response = await HandleAsync(IssueCode(CodeStore, withChallenge: false));

        Assert.True(response.Succeeded, response.Error?.ToString());
    }

    [Fact]
    public async Task RechazaUnCodigoEmitidoParaOtroCliente()
    {
        var response = await HandleAsync(IssueCode(CodeStore, clientId: "backend-service"));

        Assert.Equal("invalid_grant", response.Error?.Code);
    }

    [Fact]
    public async Task ElCodigoSeConsumeAunqueFallelaValidacionPosterior()
    {
        var code = IssueCode(CodeStore, withChallenge: true);

        var rejected = await HandleAsync(code, codeVerifier: "verificador-que-no-corresponde-XXXXXXXXXXX");

        Assert.Equal("invalid_grant", rejected.Error?.Code);
        Assert.Equal("invalid_grant", (await HandleAsync(code, codeVerifier: Verifier)).Error?.Code);
    }

    [Fact]
    public async Task ElCodigoSeConsumeAunqueElRedirectUriNoCoincida()
    {
        var code = IssueCode(CodeStore);

        var rejected = await HandleAsync(code, redirectUri: "https://localhost:5173/otro");

        Assert.Equal("invalid_grant", rejected.Error?.Code);
        Assert.Equal("invalid_grant", (await HandleAsync(code)).Error?.Code);
    }

    [Fact]
    public async Task RechazaUnCodigoCaducado()
    {
        var code = IssueCode(CodeStore);

        Clock.Advance(TimeSpan.FromMinutes(6));

        Assert.Equal("invalid_grant", (await HandleAsync(code)).Error?.Code);
    }

    [Fact]
    public async Task AceptaUnCodigoQueVenceDentroDeSuVigencia()
    {
        var code = IssueCode(CodeStore);

        Clock.Advance(TimeSpan.FromMinutes(4));

        Assert.True((await HandleAsync(code)).Succeeded);
    }

    [Fact]
    public async Task EmiteRefreshTokenSoloSiSePidioOfflineAccess()
    {
        var code = IssueCode(CodeStore, scopes: ["openid", "email"]);

        var response = await HandleAsync(code);

        Assert.Null(response.Value?.RefreshToken);
    }

    [Fact]
    public async Task EmiteRefreshTokenCuandoSePidioOfflineAccess()
    {
        var code = IssueCode(CodeStore, scopes: ["openid", "offline_access"]);

        var response = await HandleAsync(code);

        Assert.NotNull(response.Value?.RefreshToken);
    }

    [Fact]
    public async Task EmiteIdTokenPorqueElCodeLevaElScopeOpenid()
    {
        var response = await HandleAsync(IssueCode(CodeStore, scopes: ["openid", "email"]));

        Assert.NotNull(response.Value?.IdToken);
    }

    [Fact]
    public async Task NoEmiteIdTokenSiElCodeNoLlevaOpenid()
    {
        var response = await HandleAsync(IssueCode(CodeStore, scopes: ["email"]));

        Assert.Null(response.Value?.IdToken);
    }

    private async Task<Result<TokenResponse>> HandleAsync(
        AuthorizationCode? code = null,
        string codeValue = "codigo-inventado",
        string? redirectUri = null,
        string? codeVerifier = null) =>
        await _handler.HandleAsync(TokenRequest.ForAuthorizationCode(
            ClientStoreFixture.Spa(),
            Issuer,
            ["openid", "email"],
            code?.Code ?? codeValue,
            redirectUri ?? RedirectUri,
            codeVerifier));
}
