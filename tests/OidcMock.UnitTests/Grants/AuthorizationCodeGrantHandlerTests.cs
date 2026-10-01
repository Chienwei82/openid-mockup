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
    public void CanjeaUnCodigoValidoYEmiteTokens()
    {
        var response = Handle(IssueCode(CodeStore));

        Assert.True(response.Succeeded, response.Error?.ToString());
        Assert.Equal(TokenTypes.Bearer, response.Value?.TokenType);
        Assert.Equal((long)TimeSpan.FromMinutes(30).TotalSeconds, response.Value?.ExpiresIn);
    }

    [Fact]
    public void EmiteUnAccessTokenValidoParaElSubjectAutorizado()
    {
        var response = Handle(IssueCode(CodeStore));

        var result = Validator.Validate(response.Value!.AccessToken, Issuer, [ClientId]);

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal("user-1", TokenTestValidator.ReadClaim(response.Value.AccessToken, "sub").GetString());
    }

    [Fact]
    public void EmiteUnIdTokenConElNonceDeLaPeticionOriginal()
    {
        var response = Handle(IssueCode(CodeStore, nonce: "n-abc"));

        Assert.Equal("n-abc", TokenTestValidator.ReadClaim(response.Value!.IdToken!, "nonce").GetString());
    }

    [Fact]
    public void EmiteUnRefreshTokenQueSePuedeCanjear()
    {
        var response = Handle(IssueCode(CodeStore));

        var refreshed = RefreshTokens.Redeem(response.Value!.RefreshToken!);

        Assert.True(refreshed.Succeeded);
        Assert.Equal("user-1", refreshed.Value?.Subject);
    }

    [Fact]
    public void ElAccessTokenLevaLosScopesConcedidosEnElCodigo()
    {
        var response = Handle(IssueCode(CodeStore));

        Assert.Equal("openid email", response.Value?.Scope);
    }

    [Fact]
    public void RechazaUnCodigoDesconocido()
    {
        var response = Handle(codeValue: "codigo-inventado");

        Assert.Equal("invalid_grant", response.Error?.Code);
    }

    [Fact]
    public void RechazaUnCodigoYaCanjeado()
    {
        var code = IssueCode(CodeStore);
        Handle(code);

        Assert.Equal("invalid_grant", Handle(code).Error?.Code);
    }

    [Fact]
    public void RechazaUnRedirectUriQueNoCoincideConElDelCodigo()
    {
        var response = Handle(IssueCode(CodeStore), redirectUri: "https://localhost:5173/otro");

        Assert.Equal("invalid_grant", response.Error?.Code);
    }

    [Fact]
    public void ExigeElCodeVerifierCuandoElCodigoTieneCodeChallenge()
    {
        var response = Handle(IssueCode(CodeStore, withChallenge: true), codeVerifier: null);

        Assert.Equal("invalid_grant", response.Error?.Code);
    }

    [Fact]
    public void RechazaUnCodeVerifierQueNoCorrespondeAlChallenge()
    {
        var response = Handle(
            IssueCode(CodeStore, withChallenge: true),
            codeVerifier: "otro-verificador-que-no-corresponde-al-desafio-XXXXX");

        Assert.Equal("invalid_grant", response.Error?.Code);
    }

    [Fact]
    public void AceptaElCodeVerifierQueCorrespondeAlChallenge()
    {
        var response = Handle(IssueCode(CodeStore, withChallenge: true), codeVerifier: Verifier);

        Assert.True(response.Succeeded, response.Error?.ToString());
    }

    [Fact]
    public void UnCodigoSinPkceNoExigeCodeVerifier()
    {
        var response = Handle(IssueCode(CodeStore, withChallenge: false));

        Assert.True(response.Succeeded, response.Error?.ToString());
    }

    [Fact]
    public void RechazaUnCodigoEmitidoParaOtroCliente()
    {
        var response = Handle(IssueCode(CodeStore, clientId: "backend-service"));

        Assert.Equal("invalid_grant", response.Error?.Code);
    }

    private Result<TokenResponse> Handle(
        AuthorizationCode? code = null,
        string codeValue = "codigo-inventado",
        string? redirectUri = null,
        string? codeVerifier = null) =>
        _handler.Handle(new TokenRequest(
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
