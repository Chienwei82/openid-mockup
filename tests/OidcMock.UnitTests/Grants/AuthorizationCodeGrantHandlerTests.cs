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
        _handler = new AuthorizationCodeGrantHandler(CodeStore, RefreshTokens, UserStore, Tokens, Clock);

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

        var result = Validator.Validate(response.Value!.AccessToken, Issuer, [ClientId]);

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
        var response = await HandleAsync(IssueCode(CodeStore));

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

    private async Task<Result<TokenResponse>> HandleAsync(
        AuthorizationCode? code = null,
        string codeValue = "codigo-inventado",
        string? redirectUri = null,
        string? codeVerifier = null) =>
        await _handler.HandleAsync(new TokenRequest(
            ClientStoreFixture.Spa(),
            Issuer,
            ["openid", "email"],
            code?.Code ?? codeValue,
            redirectUri ?? RedirectUri,
            codeVerifier,
            null,
            null,
            null));
}
