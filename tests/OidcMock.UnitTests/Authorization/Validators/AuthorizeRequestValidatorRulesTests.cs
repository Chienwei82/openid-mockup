using OidcMock.Core.Authorization;
using OidcMock.Core.Codes;
using OidcMock.Core.Grants;
using OidcMock.Core.Scopes;
using OidcMock.UnitTests.Fixtures;
using static OidcMock.UnitTests.Fixtures.AuthorizeValidation;

namespace OidcMock.UnitTests.Authorization.Validators;

/// <summary>Regla: el client_id debe existir en el store de clientes.</summary>
public sealed class ClientExistsValidatorTests
{
    private readonly ClientExistsValidator _validator = new();

    [Fact]
    public void AceptaUnClienteResuelto() => Assert.Null(_validator.Validate(Context()));

    [Fact]
    public void RechazaUnClientIdDesconocido() =>
        Assert.Equal(
            "invalid_client",
            _validator.Validate(Context(client: null, clientId: "cliente-inexistente"))?.Code);

    [Fact]
    public void SuErrorNoSeRedirigePorqueElClienteNoEstaVerificado() =>
        Assert.False(_validator.ErrorIsRedirectable);
}

/// <summary>Regla: el redirect_uri debe estar registrado para ese cliente, con comparacion exacta.</summary>
public sealed class RedirectUriValidatorTests
{
    private readonly RedirectUriValidator _validator = new();

    [Fact]
    public void AceptaUnRedirectUriRegistrado() => Assert.Null(_validator.Validate(Context()));

    [Fact]
    public void RechazaUnRedirectUriAjenoAlCliente() =>
        Assert.Equal(
            "invalid_request",
            _validator.Validate(Context(redirectUri: "https://atacante.example/callback"))?.Code);

    [Fact]
    public void RechazaUnRedirectUriAusente() =>
        Assert.Equal("invalid_request", _validator.Validate(Context(redirectUri: null))?.Code);

    [Fact]
    public void LaComparacionEsExactaYNoAdmitePrefijos() =>
        Assert.Equal("invalid_request", _validator.Validate(Context(redirectUri: $"{RedirectUri}/extra"))?.Code);

    [Fact]
    public void SuErrorNoSeRedirigePorqueLaUrlNoEstaVerificada() =>
        Assert.False(_validator.ErrorIsRedirectable);
}

/// <summary>Regla: cada scope solicitado tiene que estar permitido para el cliente.</summary>
public sealed class AllowedScopesValidatorTests
{
    private readonly AllowedScopesValidator _validator = new(ClientStoreFixture.Create());

    [Fact]
    public void AceptaScopesPermitidos() => Assert.Null(_validator.Validate(Context()));

    [Fact]
    public void RechazaUnScopeNoPermitidoParaElCliente() =>
        Assert.Equal("invalid_scope", _validator.Validate(Context(scopes: [ScopeNames.OpenId, "scope-prohibido"]))?.Code);

    [Fact]
    public void SuErrorSiSeRedirigePorqueElRedirectUriYaEstaVerificado() =>
        Assert.True(_validator.ErrorIsRedirectable);
}

/// <summary>Regla: la peticion debe traer el scope openid, que es lo que la convierte en OIDC.</summary>
public sealed class OpenIdScopeValidatorTests
{
    private readonly OpenIdScopeValidator _validator = new();

    [Fact]
    public void AceptaUnaPeticionConOpenid() => Assert.Null(_validator.Validate(Context()));

    [Fact]
    public void RechazaUnaPeticionSinOpenid() =>
        Assert.Equal("invalid_scope", _validator.Validate(Context(scopes: [ScopeNames.Email]))?.Code);
}

/// <summary>Regla: si el cliente exige PKCE, la peticion debe traer code_challenge.</summary>
public sealed class PkceRequiredValidatorTests
{
    private readonly PkceRequiredValidator _validator = new();

    [Fact]
    public void AceptaPkceCuandoElClienteLoExige() => Assert.Null(_validator.Validate(Context()));

    [Fact]
    public void RechazaUnaPeticionSinCodeChallenge() =>
        Assert.Equal("invalid_request", _validator.Validate(Context(codeChallenge: null))?.Code);

    [Fact]
    public void AceptaNoEnviarPkceSiElClienteNoLoExige() =>
        Assert.Null(_validator.Validate(Context(
            client: ClientStoreFixture.Service() with { RequirePkce = false },
            codeChallenge: null)));
}

/// <summary>Regla: el code_challenge_method solo puede ser plain o S256.</summary>
public sealed class CodeChallengeMethodValidatorTests
{
    private readonly CodeChallengeMethodValidator _validator = new();

    [Theory]
    [InlineData(PkceCodeChallengeMethods.Plain)]
    [InlineData(PkceCodeChallengeMethods.Sha256)]
    [InlineData(null)]
    public void AceptaLosMetodosSoportados(string? method) =>
        Assert.Null(_validator.Validate(Context(codeChallengeMethod: method)));

    [Theory]
    [InlineData("MD5")]
    [InlineData("")]
    public void RechazaUnMetodoNoSoportado(string method) =>
        Assert.Equal("invalid_request", _validator.Validate(Context(codeChallengeMethod: method))?.Code);

    [Fact]
    public void RechazaUnMetodoSinCodeChallenge() =>
        Assert.Equal(
            "invalid_request",
            _validator.Validate(Context(codeChallenge: null, codeChallengeMethod: PkceCodeChallengeMethods.Sha256))?.Code);
}

/// <summary>Regla: el response_mode solo puede ser query, fragment o form_post.</summary>
public sealed class ResponseModeValidatorTests
{
    private readonly ResponseModeValidator _validator = new();

    [Theory]
    [InlineData(ResponseModes.Query)]
    [InlineData(ResponseModes.Fragment)]
    [InlineData(ResponseModes.FormPost)]
    public void AceptaLosResponseModesSoportados(string mode) =>
        Assert.Null(_validator.Validate(Context(responseMode: mode)));

    [Theory]
    [InlineData("form_get")]
    [InlineData("")]
    public void RechazaUnResponseModeNoSoportado(string mode) =>
        Assert.Equal("invalid_request", _validator.Validate(Context(responseMode: mode))?.Code);
}

/// <summary>Regla: el prompt tiene que estar entre los que anuncia el discovery.</summary>
public sealed class PromptValidatorTests
{
    private readonly PromptValidator _validator = new();

    [Theory]
    [InlineData(PromptValues.None)]
    [InlineData(PromptValues.Login)]
    [InlineData(PromptValues.Consent)]
    [InlineData(PromptValues.SelectAccount)]
    [InlineData(null)]
    [InlineData(" ")]
    public void AceptaLosPromptsAnunciadosPorElDiscovery(string? prompt) =>
        Assert.Null(_validator.Validate(Context(prompt: prompt)));

    [Fact]
    public void RechazaUnPromptDesconocido() =>
        Assert.Equal("invalid_request", _validator.Validate(Context(prompt: "prompt_inventado"))?.Code);
}

/// <summary>Regla: el grant_type tiene que estar permitido para el cliente.</summary>
public sealed class GrantTypeValidatorTests
{
    private readonly GrantTypeValidator _validator = new();

    [Fact]
    public void AceptaElGrantTypePermitido() =>
        Assert.Null(_validator.Validate(Context(grantType: GrantTypes.AuthorizationCode)));

    [Fact]
    public void RechazaUnGrantTypeNoPermitido() =>
        Assert.Equal("unauthorized_client", _validator.Validate(Context(grantType: GrantTypes.ClientCredentials))?.Code);
}
