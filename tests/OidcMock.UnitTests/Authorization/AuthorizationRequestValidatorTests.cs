using OidcMock.Core.Authorization;
using OidcMock.Core.Errors;
using OidcMock.UnitTests.Fixtures;

namespace OidcMock.UnitTests.Authorization;

public sealed class AuthorizationRequestValidatorTests
{
    private const string ValidRedirectUri = "https://localhost:5173/callback";
    private const string ValidChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    private readonly AuthorizationRequestValidator _validator = new(ClientStoreFixture.Create(), ScopeStoreFixture.Create());

    [Fact]
    public void AceptaUnaPeticionDeAutorizacionValida()
    {
        var result = Validate();

        Assert.True(result.Succeeded, result.Error?.ToString());
        Assert.Equal("web-app-spa", result.Value?.Client.ClientId);
        Assert.Equal(ValidRedirectUri, result.Value?.RedirectUri);
        Assert.Equal(["openid", "email"], result.Value?.Scopes);
    }

    [Fact]
    public void RechazaUnClientIdDesconocidoSinRedirigir()
    {
        var result = Validate(clientId: "cliente-inexistente");

        Assert.True(result.IsError);
        Assert.Equal("invalid_client", result.Error?.Code);
        Assert.Equal(400, result.Error?.StatusCode);
    }

    [Fact]
    public void RechazaUnRedirectUriNoRegistradoSinRedirigir()
    {
        var result = Validate(redirectUri: "https://atacante.example/callback");

        Assert.True(result.IsError);
        Assert.Equal("invalid_request", result.Error?.Code);
        Assert.Equal(400, result.Error?.StatusCode);
    }

    [Fact]
    public void RechazaUnRedirectUriAusenteSinRedirigir()
    {
        var result = Validate(redirectUri: null);

        Assert.True(result.IsError);
        Assert.Equal("invalid_request", result.Error?.Code);
    }

    [Fact]
    public void UnErrorDeRedirectUriValidoSeDevuelveParaRedirigirlo()
    {
        var result = Validate(responseType: "token_inventado");

        Assert.True(result.IsError);
        Assert.Equal(ValidRedirectUri, result.RedirectUri);
    }

    [Fact]
    public void ReenviaElStateEnElErrorCuandoElRedirectUriEsValido()
    {
        var result = Validate(responseType: "token_inventado", state: "st-123");

        Assert.Equal("st-123", result.State);
    }

    [Fact]
    public void NoRedirigeErroresDeClientIdNiDeRedirectUri()
    {
        Assert.Null(Validate(clientId: "inexistente").RedirectUri);
        Assert.Null(Validate(redirectUri: "https://atacante.example/callback").RedirectUri);
    }

    [Fact]
    public void RechazaUnScopeQueElClienteNoTienePermitido()
    {
        var result = Validate(scopes: ["openid", "scope-prohibido"]);

        Assert.Equal("invalid_scope", result.Error?.Code);
    }

    [Fact]
    public void RechazaUnScopeQueNoExisteEnElMock()
    {
        var result = Validate(scopes: ["openid", "scope.inventado"]);

        Assert.Equal("invalid_scope", result.Error?.Code);
    }

    [Fact]
    public void SinScopesUsaOpenid()
    {
        var result = Validate(scopes: []);

        Assert.True(result.Succeeded);
        Assert.Equal(["openid"], result.Value?.Scopes);
    }

    [Fact]
    public void ExigePkceCuandoElClienteLoRequiere()
    {
        var result = Validate(codeChallenge: null);

        Assert.Equal("invalid_request", result.Error?.Code);
    }

    [Fact]
    public void AceptaPkceSimple()
    {
        var result = Validate(codeChallenge: "desafio-en-claro", codeChallengeMethod: "plain");

        Assert.True(result.Succeeded, result.Error?.ToString());
        Assert.Equal("plain", result.Value?.CodeChallengeMethod);
    }

    [Fact]
    public void SinCodeChallengeMethodUsaPlainQueEsElDefaultDeLaEspecificacion()
    {
        var result = Validate();

        Assert.True(result.Succeeded, result.Error?.ToString());
        Assert.Equal("plain", result.Value?.CodeChallengeMethod);
    }

    [Fact]
    public void RechazaUnMetodoDeCodeChallengeNoSoportado()
    {
        var result = Validate(codeChallenge: "desafio", codeChallengeMethod: "MD5");

        Assert.Equal("invalid_request", result.Error?.Code);
    }

    [Fact]
    public void AceptaPkceConS256CuandoElClienteLoPide()
    {
        var result = Validate(codeChallengeMethod: "S256");

        Assert.True(result.Succeeded, result.Error?.ToString());
        Assert.Equal("S256", result.Value?.CodeChallengeMethod);
    }

    [Fact]
    public void PropagaNonceYState()
    {
        var result = Validate(nonce: "n-abc", state: "st-123");

        Assert.Equal("n-abc", result.Value?.Nonce);
        Assert.Equal("st-123", result.Value?.State);
    }

    [Fact]
    public void RechazaPromptDesconocido()
    {
        var result = Validate(prompt: "prompt_inventado");

        Assert.Equal("invalid_request", result.Error?.Code);
    }

    [Fact]
    public void AceptaLosPromptsQueAnunciaElDiscovery()
    {
        foreach (var prompt in new[] { "none", "login", "consent", "select_account" })
        {
            Assert.True(Validate(prompt: prompt).Succeeded, $"prompt={prompt}");
        }
    }

    [Fact]
    public void UnPromptEnBlancoSeTrataComoPromptAusente()
    {
        var result = Validate(prompt: " ");

        Assert.True(result.Succeeded, result.Error?.ToString());
        Assert.Null(result.Value?.Prompt);
    }

    [Fact]
    public void RechazaUnGrantTypeQueElClienteNoPermite()
    {
        var result = Validate(grantType: "client_credentials");

        Assert.Equal("unauthorized_client", result.Error?.Code);
    }

    private AuthorizationValidationResult Validate(
        string? clientId = "web-app-spa",
        string? redirectUri = ValidRedirectUri,
        string responseType = "code",
        IReadOnlyList<string>? scopes = null,
        string? nonce = null,
        string? state = null,
        string? codeChallenge = ValidChallenge,
        string? codeChallengeMethod = null,
        string? prompt = null,
        string? grantType = "authorization_code") =>
        _validator.Validate(new AuthorizationRequest(
            clientId,
            redirectUri,
            responseType,
            scopes ?? ["openid", "email"],
            nonce,
            state,
            codeChallenge,
            codeChallengeMethod,
            prompt,
            grantType,
            ResponseModes.Query));
}