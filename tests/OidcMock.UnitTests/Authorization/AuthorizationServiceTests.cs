using OidcMock.Core.Authorization;
using OidcMock.Core.Codes;
using Microsoft.Extensions.Time.Testing;
using OidcMock.UnitTests.Fixtures;

namespace OidcMock.UnitTests.Authorization;

/// <summary>
/// Tests de la emision del codigo de autorizacion. Cada test fija un dato que el canje posterior
/// necesita, porque un codigo incompleto rompe el flujo entero mas adelante y el fallo aparece lejos
/// de su causa.
/// </summary>
public sealed class AuthorizationServiceTests
{
    private const string RedirectUri = "https://localhost:5173/callback";
    private const string UserName = "jperez";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryCodeStore _codeStore;
    private readonly AuthorizationService _service;

    public AuthorizationServiceTests()
    {
        _codeStore = new InMemoryCodeStore(_clock);
        _service = new AuthorizationService(_codeStore);
    }

    [Fact]
    public void AprobarEmiteElCodigoDeAutorizacion()
    {
        var granted = Approve();

        Assert.True(granted.Succeeded, granted.Error?.ToString());
        Assert.False(string.IsNullOrWhiteSpace(granted.Value?.Code.Code));
    }

    /// <summary>
    /// Regresion: el codigo debe llevar el redirect_uri con el que se aprobo. Sin el, el canje en el
    /// token endpoint falla con invalid_grant porque no puede verificar la coincidencia exigida por
    /// RFC 6749 4.1.3.
    /// </summary>
    [Fact]
    public void ElCodigoGuardaElRedirectUriDeLaPeticionAprobada()
    {
        var granted = Approve();

        Assert.Equal(RedirectUri, granted.Value?.Code.RedirectUri);
    }

    [Fact]
    public void ElCodigoGuardaElClientIdYLosScopesAprobados()
    {
        var granted = Approve(scopes: ["openid", "email"]);

        Assert.Equal(ClientStoreFixture.SpaClientId, granted.Value?.Code.ClientId);
        Assert.Equal(["openid", "email"], granted.Value?.Code.Scopes);
    }

    [Fact]
    public void ElCodigoGuardaNonceStateYCodigoPkce()
    {
        var granted = Approve(nonce: "n-1", state: "st-1", codeChallenge: "desafio", codeChallengeMethod: "S256");
        var code = granted.Value!.Code;

        Assert.Equal("n-1", code.Nonce);
        Assert.Equal("st-1", code.State);
        Assert.Equal("desafio", code.CodeChallenge);
        Assert.Equal("S256", code.CodeChallengeMethod);
    }

    [Fact]
    public void ElCodigoExpiraConLaVigenciaDelCliente()
    {
        var granted = Approve();

        Assert.Equal(TimeSpan.FromMinutes(5), granted.Value!.Code.ExpiresAt - granted.Value.Code.AuthenticatedAt);
    }

    private Core.Errors.Result<AuthorizationGranted> Approve(
        IReadOnlyList<string>? scopes = null,
        string? nonce = null,
        string? state = null,
        string? codeChallenge = null,
        string? codeChallengeMethod = null) =>
        _service.Approve(new AuthorizationApproval(
            UserName,
            ValidAuthorization(scopes, nonce, state, codeChallenge, codeChallengeMethod)));

    private static ValidatedAuthorizationRequest ValidAuthorization(
        IReadOnlyList<string>? scopes = null,
        string? nonce = null,
        string? state = null,
        string? codeChallenge = null,
        string? codeChallengeMethod = null) =>
        new(
            ClientStoreFixture.Spa(),
            RedirectUri,
            scopes ?? ["openid"],
            ResponseTypeNames.Code,
            ResponseModes.Query,
            nonce,
            state,
            codeChallenge,
            codeChallengeMethod,
            Prompt: null);
}