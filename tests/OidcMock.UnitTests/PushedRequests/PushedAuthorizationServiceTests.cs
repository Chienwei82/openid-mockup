using Microsoft.Extensions.Time.Testing;
using OidcMock.Core.Authorization;
using OidcMock.Core.Authorization.Validators;
using OidcMock.Core.Clients;
using OidcMock.Core.Codes;
using OidcMock.Core.PendingRequests;
using OidcMock.Core.PushedRequests;
using OidcMock.UnitTests.Fixtures;

namespace OidcMock.UnitTests.PushedRequests;

/// <summary>
/// PAR (RFC 9126) es el unico servicio de Core sin pruebas unitarias propias: lo cubrian dos tests de
/// compatibilidad de extremo a extremo, asi que si la validacion o la caducidad se rompieran, la suite
/// seguiria en verde.
///
/// Lo que se comprueba aqui es el contrato observable del servicio: que empuja una peticion valida,
/// que rechaza cada clase de peticion invalida, que la caducidad sale del reloj inyectado y que el
/// <c>request_uri</c> es de un solo uso.
/// </summary>
public sealed class PushedAuthorizationServiceTests
{
    private const string RedirectUri = "https://localhost:5173/callback";
    private const string CodeChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryPendingAuthorizationStore _store;
    private readonly PushedAuthorizationService _service;

    public PushedAuthorizationServiceTests()
    {
        var clients = ClientStoreFixture.Create();
        _store = new InMemoryPendingAuthorizationStore(_clock);
        _service = new PushedAuthorizationService(_store, ValidatorOver(clients), clients, _clock);
    }

    /// <summary>
    /// La misma cadena de reglas que registra el host, en el mismo orden: una prueba que usara una
    /// cadena de mentira podria pasar mientras PAR se rompe con la de verdad.
    /// </summary>
    private static AuthorizationRequestValidator ValidatorOver(IClientStore clients) =>
        new AuthorizationRequestValidator(clients,
        [
            new ClientExistsValidator(),
            new RedirectUriValidator(),
            new GrantTypeValidator(),
            new ResponseTypeValidator(),
            new OpenIdScopeValidator(),
            new AllowedScopesValidator(),
            new KnownScopesValidator(ScopeStoreFixture.Create()),
            new PkceRequiredValidator(),
            new CodeChallengeMethodValidator(),
            new PromptValidator(),
            new ResponseModeValidator(),
        ]);

    [Fact]
    public void EmpujaLaPeticionYDevuelveUnRequestUri()
    {
        var pushed = _service.Push(ValidParameters());

        Assert.True(pushed.Succeeded, pushed.Error?.ToString());
        Assert.False(string.IsNullOrWhiteSpace(pushed.Value!.RequestUri));
    }

    /// <summary>
    /// RFC 9126 2.2: el <c>expires_in</c> tiene que servir al cliente para saber cuando tiene que
    /// empujar de nuevo, asi que se compara con la caducidad real que guardo el store.
    /// </summary>
    [Fact]
    public void ElExpiresInCoincideConLaCaducidadGuardada()
    {
        var pushed = _service.Push(ValidParameters());

        var pending = _store.Find(pushed.Value!.RequestUri);

        Assert.True(pending.Succeeded);
        Assert.Equal(pushed.Value.ExpiresIn, (int)(pending.Value!.ExpiresAt - _clock.GetUtcNow()).TotalSeconds);
    }

    [Fact]
    public void LaPeticionEmpujadaSeReconstruyeIgualQueComoSeEmpujo()
    {
        var pushed = _service.Push(ValidParameters(state: "st-original", nonce: "nonce-original"));

        var found = _service.Find(pushed.Value!.RequestUri);

        Assert.True(found.Succeeded);
        var request = found.Value!;
        Assert.Equal("st-original", request.State);
        Assert.Equal("nonce-original", request.Nonce);
        Assert.Equal(RedirectUri, request.RedirectUri);
        Assert.Contains("openid", request.Scopes);
        Assert.Contains("email", request.Scopes);
    }

    /// <summary>
    /// RFC 9126 4: el <c>request_uri</c> es de un solo uso. Si se pudiera reutilizar, una sola peticion
    /// empujada generaria todos los codigos que el cliente quisiera.
    /// </summary>
    [Fact]
    public void ElRequestUriEsDeUnSoloUso()
    {
        var pushed = _service.Push(ValidParameters());

        _service.Consume(pushed.Value!.RequestUri);

        Assert.False(_service.Find(pushed.Value.RequestUri).Succeeded);
    }

    [Fact]
    public void UnRequestUriDesconocidoNoSeReconstruye()
    {
        var found = _service.Find("urn:inventado");

        Assert.False(found.Succeeded);
        Assert.Equal("invalid_request", found.Error?.Code);
    }

    [Fact]
    public void UnRequestUriVacioNoSeReconstruye()
    {
        Assert.False(_service.Find(string.Empty).Succeeded);
    }

    /// <summary>
    /// La caducidad es del reloj inyectado, no del reloj del sistema: sin esto, un <c>request_uri</c>
    /// caducado no se puede comprobar en una prueba sin esperar cinco minutos.
    /// </summary>
    [Fact]
    public void UnRequestUriCaducadoDesapareceAlPasarSuVida()
    {
        var pushed = _service.Push(ValidParameters());

        _clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        Assert.False(_service.Find(pushed.Value!.RequestUri).Succeeded);
    }

    [Fact]
    public void UnRequestUriVigenteAunSeReconstruye()
    {
        var pushed = _service.Push(ValidParameters());

        _clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));

        Assert.True(_service.Find(pushed.Value!.RequestUri).Succeeded);
    }

    [Fact]
    public void RechazaUnClienteConfidencialConSecretoIncorrecto()
    {
        var pushed = _service.Push(ValidParameters(
            clientId: ClientStoreFixture.ConfidentialWebClientId,
            clientSecret: "no-es-el-secreto"));

        Assert.False(pushed.Succeeded);
        Assert.Equal("invalid_client", pushed.Error?.Code);
    }

    [Fact]
    public void AceptaUnClienteConfidencialConSuSecreto()
    {
        var pushed = _service.Push(ValidParameters(
            clientId: ClientStoreFixture.ConfidentialWebClientId,
            clientSecret: "super-secreto-web"));

        Assert.True(pushed.Succeeded, pushed.Error?.ToString());
    }

    [Fact]
    public void RechazaUnClienteDesconocido()
    {
        var pushed = _service.Push(ValidParameters(clientId: "nadie"));

        Assert.False(pushed.Succeeded);
        Assert.Equal("invalid_client", pushed.Error?.Code);
    }

    [Fact]
    public void RechazaUnRedirectUriNoRegistrada()
    {
        var pushed = _service.Push(ValidParameters(redirectUri: "https://atacante.example/callback"));

        Assert.False(pushed.Succeeded);
        Assert.Equal("invalid_request", pushed.Error?.Code);
    }

    [Fact]
    public void RechazaUnScopeQueElClienteNoTienePermitido()
    {
        var pushed = _service.Push(ValidParameters(scope: "openid direccion"));

        Assert.False(pushed.Succeeded);
        Assert.Equal("invalid_scope", pushed.Error?.Code);
    }

    /// <summary>
    /// Un push sin challenge de un cliente que exige PKCE tiene que rechazarse ya: si se aceptara, el
    /// authorize volveria a validar y el fallo llegaria tarde, con el usuario delante.
    /// </summary>
    [Fact]
    public void RechazaUnPushSinCodeChallengeDeUnClienteQueLoExige()
    {
        var pushed = _service.Push(ValidParameters(codeChallenge: null));

        Assert.False(pushed.Succeeded);
        Assert.Equal("invalid_request", pushed.Error?.Code);
    }

    [Fact]
    public void SinScopeSeEmpujaConOpenid()
    {
        var pushed = _service.Push(ValidParameters(scope: null));

        Assert.True(pushed.Succeeded, pushed.Error?.ToString());
        Assert.Equal(["openid"], _store.Find(pushed.Value!.RequestUri).Value!.Scopes);
    }

    /// <summary>
    /// El code_challenge_method ausente cuenta como <c>plain</c> (RFC 7636 4.3), igual que en el
    /// authorize directo: si PAR lo perdiera, el canje del codigo fallaria.
    /// </summary>
    [Fact]
    public void SinCodeChallengeMethodElChallengeQuedaComoPlain()
    {
        var pushed = _service.Push(ValidParameters(codeChallengeMethod: null));

        var found = _service.Find(pushed.Value!.RequestUri);

        Assert.True(found.Succeeded);
        Assert.Equal("plain", found.Value!.CodeChallengeMethod);
    }

    private static PushRequestParameters ValidParameters(
        string clientId = ClientStoreFixture.SpaClientId,
        string? clientSecret = null,
        string redirectUri = RedirectUri,
        string? scope = "openid email",
        string? state = null,
        string? nonce = null,
        string? codeChallenge = CodeChallenge,
        string? codeChallengeMethod = PkceCodeChallengeMethods.Sha256) =>
        new(
            clientId,
            clientSecret,
            redirectUri,
            ResponseTypeNames.Code,
            scope,
            state,
            nonce,
            codeChallenge,
            codeChallengeMethod,
            ResponseModes.Query,
            Prompt: null);
}