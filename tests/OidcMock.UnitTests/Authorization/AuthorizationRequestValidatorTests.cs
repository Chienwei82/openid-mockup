using OidcMock.Core.Authorization;
using OidcMock.Core.Codes;
using OidcMock.Core.Grants;
using OidcMock.Core.Scopes;
using OidcMock.UnitTests.Fixtures;
using static OidcMock.UnitTests.Fixtures.AuthorizeValidation;

namespace OidcMock.UnitTests.Authorization;

/// <summary>
/// Comportamiento de la cadena: orden de las reglas, primer fallo y cuando el error puede
/// viajar por el redirect_uri. Cada regla por separado tiene su propia clase de test.
/// </summary>
public sealed class AuthorizationRequestValidatorTests
{
    private readonly AuthorizationRequestValidator _validator = new(
        ClientStoreFixture.Create(),
        ScopeStoreFixture.Create(),
        [
            new ClientExistsValidator(),
            new RedirectUriValidator(),
            new GrantTypeValidator(),
            new ResponseTypeValidator(),
            new OpenIdScopeValidator(),
            new AllowedScopesValidator(ClientStoreFixture.Create()),
            new PkceRequiredValidator(),
            new CodeChallengeMethodValidator(),
            new PromptValidator(),
            new ResponseModeValidator()
        ]);

    [Fact]
    public void AceptaUnaPeticionDeAutorizacionValida()
    {
        var result = _validator.Validate(Context().Request);

        Assert.True(result.Succeeded, result.Error?.ToString());
        Assert.Equal("web-app-spa", result.Value?.Client.ClientId);
        Assert.Equal(RedirectUri, result.Value?.RedirectUri);
        Assert.Equal([ScopeNames.OpenId, ScopeNames.Email], result.Value?.Scopes);
    }

    [Fact]
    public void RechazaUnClientIdDesconocidoSinRedirigir()
    {
        var result = _validator.Validate(Context(clientId: "cliente-inexistente").Request);

        Assert.True(result.IsError);
        Assert.Equal("invalid_client", result.Error?.Code);
        Assert.Equal(400, result.Error?.StatusCode);
        Assert.Null(result.RedirectUri);
    }

    [Fact]
    public void NoRedirigeErroresDeClientIdNiDeRedirectUri()
    {
        Assert.Null(_validator.Validate(Context(clientId: "inexistente").Request).RedirectUri);
        Assert.Null(_validator.Validate(Context(redirectUri: "https://atacante.example/cb").Request).RedirectUri);
    }

    [Fact]
    public void RedirigeLosErroresSobreLaPeticionCuandoElRedirectUriYaEsValido()
    {
        var result = _validator.Validate(Context(responseType: "token_inventado", state: "st-123").Request);

        Assert.Equal(RedirectUri, result.RedirectUri);
        Assert.Equal("st-123", result.State);
    }

    [Fact]
    public void SeQuedaConElPrimerErrorDeLaCadena()
    {
        // Redirect_uri invalido y response_type invalido a la vez: manda el primero de la cadena.
        var result = _validator.Validate(Context(
            redirectUri: "https://atacante.example/cb",
            responseType: "token_inventado").Request);

        Assert.Equal("invalid_request", result.Error?.Code);
        Assert.Null(result.RedirectUri);
    }

    [Fact]
    public void SinScopesUsaOpenidYLoAcepta() =>
        Assert.Equal(
            [ScopeNames.OpenId],
            _validator.Validate(Context(scopes: []).Request).Value?.Scopes);

    [Fact]
    public void PropagaNonceYStateYDefaultaPlainElMetodoDePkce()
    {
        var result = _validator.Validate(Context(nonce: "n-abc", state: "st-123").Request);

        Assert.Equal("n-abc", result.Value?.Nonce);
        Assert.Equal("st-123", result.Value?.State);
        Assert.Equal(PkceCodeChallengeMethods.Plain, result.Value?.CodeChallengeMethod);
    }

    [Fact]
    public void RechazaUnScopeQueNoExisteEnElMock() =>
        Assert.Equal(
            "invalid_scope",
            _validator.Validate(Context(scopes: [ScopeNames.OpenId, "scope.inventado"]).Request).Error?.Code);

    [Fact]
    public void AceptaPkceSimpleYConS256()
    {
        Assert.Equal(
            PkceCodeChallengeMethods.Plain,
            _validator.Validate(Context(codeChallengeMethod: PkceCodeChallengeMethods.Plain).Request).Value?.CodeChallengeMethod);
        Assert.Equal(
            PkceCodeChallengeMethods.Sha256,
            _validator.Validate(Context(codeChallengeMethod: PkceCodeChallengeMethods.Sha256).Request).Value?.CodeChallengeMethod);
    }

    [Fact]
    public void RechazaUnGrantTypeQueElClienteNoPermite() =>
        Assert.Equal(
            "unauthorized_client",
            _validator.Validate(Context(grantType: GrantTypes.ClientCredentials).Request).Error?.Code);

    [Fact]
    public void UnPromptEnBlancoSeTrataComoPromptAusente() =>
        Assert.Null(_validator.Validate(Context(prompt: " ").Request).Value?.Prompt);
}
