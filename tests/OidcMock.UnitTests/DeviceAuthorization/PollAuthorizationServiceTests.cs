using Microsoft.Extensions.Time.Testing;
using OidcMock.Core.Clients;
using OidcMock.Core.DeviceAuthorization;
using OidcMock.Core.Grants;
using OidcMock.Core.PendingRequests;
using OidcMock.Core.Users;
using OidcMock.UnitTests.Fixtures;
using OidcMock.UnitTests.Grants;

namespace OidcMock.UnitTests.DeviceAuthorization;

/// <summary>
/// Device authorization (RFC 8628) y CIBA comparten este servicio: los dos autentican al cliente,
/// resuelven sus scopes y dejan una peticion pendiente con caducidad que luego sondea
/// <c>PollGrantHandler</c>.
///
/// Lo que fijan estas pruebas es el comportamiento que cambio al mover la logica del endpoint a Core:
/// la autenticacion del cliente (que antes no existia) y la caducidad por reloj inyectado (que antes
/// salia de <c>DateTimeOffset.UtcNow</c> y por tanto no era comprobable).
/// </summary>
public sealed class PollAuthorizationServiceTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryPendingAuthorizationStore _store;
    private readonly PollAuthorizationService _service;

    public PollAuthorizationServiceTests()
    {
        var clients = ClientStoreFixture.Create();
        _store = new InMemoryPendingAuthorizationStore(_clock);
        _service = new PollAuthorizationService(
            _store,
            ClientStoreFixture.AuthenticatorOver(clients),
            new InMemoryUserStore(SampleUser()),
            _clock);
    }

    [Fact]
    public void EmiteUnDeviceCodeConSuUserCodeYSuUrisDeVerificacion()
    {
        var started = _service.StartDevice(SpaDevice());

        Assert.True(started.Succeeded, started.Error?.ToString());
        var device = started.Value!;
        Assert.False(string.IsNullOrWhiteSpace(device.DeviceCode));
        Assert.False(string.IsNullOrWhiteSpace(device.UserCode));
        Assert.Equal(VerificationUri.Constant, device.VerificationUri);
    }

    /// <summary>
    /// RFC 8628 3.2: el intervalo que anuncia es el que el cliente debe respetar al sonear. Si el store
    /// no lo guardara, <c>slow_down</c> no tendria nada contra lo que comparar.
    /// </summary>
    [Fact]
    public void ElIntervaloAnunciadoEsElQueGuardaElStore()
    {
        var started = _service.StartDevice(SpaDevice());

        var pending = _store.Find(started.Value!.DeviceCode);

        Assert.True(pending.Succeeded);
        Assert.Equal(TimeSpan.FromSeconds(started.Value.Interval), pending.Value!.Interval);
    }

    [Fact]
    public void ElExpiresInCoincideConLaCaducidadGuardada()
    {
        var started = _service.StartDevice(SpaDevice());

        var pending = _store.Find(started.Value!.DeviceCode);

        Assert.Equal(
            started.Value.ExpiresIn,
            (int)(pending.Value!.ExpiresAt - _clock.GetUtcNow()).TotalSeconds);
    }

    /// <summary>
    /// La caducidad era del reloj del sistema antes de mover la logica a Core. Con el reloj inyectado,
    /// un device code caducado se puede comprobar sin esperar diez minutos.
    /// </summary>
    [Fact]
    public void ElDeviceCodeCaducaPasadaSuVida()
    {
        var started = _service.StartDevice(SpaDevice());

        _clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        Assert.False(_store.Find(started.Value!.DeviceCode).Succeeded);
    }

    [Fact]
    public void ElDeviceCodeSigueVigenteAntesDeCaducar()
    {
        var started = _service.StartDevice(SpaDevice());

        _clock.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));

        Assert.True(_store.Find(started.Value!.DeviceCode).Succeeded);
    }

    [Fact]
    public void SinScopeSeConcedeOpenid()
    {
        var started = _service.StartDevice(new DeviceAuthorizationStart(SpaCredentials(), Scope: null));

        Assert.True(started.Succeeded, started.Error?.ToString());
        Assert.Equal(["openid"], _store.Find(started.Value!.DeviceCode).Value!.Scopes);
    }

    [Fact]
    public void RechazaUnScopeNoPermitidoParaElCliente()
    {
        var started = _service.StartDevice(new DeviceAuthorizationStart(SpaCredentials(), "openid direccion"));

        Assert.False(started.Succeeded);
        Assert.Equal("invalid_scope", started.Error?.Code);
    }

    /// <summary>
    /// El defecto que motivo mover esto a Core: device authorization no autenticaba al cliente, asi que
    /// un cliente confidencial conseguia un device code con un secreto equivocado.
    /// </summary>
    [Fact]
    public void RechazaAUnClienteConfidencialConSecretoIncorrecto()
    {
        var started = _service.StartDevice(new DeviceAuthorizationStart(
            ConfidentialCredentials("no-es-el-secreto"),
            "openid"));

        Assert.False(started.Succeeded);
        Assert.Equal("invalid_client", started.Error?.Code);
    }

    [Fact]
    public void RechazaAUnClienteDesconocido()
    {
        var started = _service.StartDevice(new DeviceAuthorizationStart(
            new ClientCredentials(ClientAuthenticationMethods.ClientSecretPost, "nadie", null),
            "openid"));

        Assert.False(started.Succeeded);
        Assert.Equal("invalid_client", started.Error?.Code);
    }

    [Fact]
    public void EmiteCibaConUnAuthReqId()
    {
        var started = _service.StartCiba(CibaRequest(loginHint: "jperez"));

        Assert.True(started.Succeeded, started.Error?.ToString());
        Assert.False(string.IsNullOrWhiteSpace(started.Value!.AuthReqId));
    }

    /// <summary>
    /// El camino feliz de CIBA en el mock: si el <c>login_hint</c> identifica a alguien del store, la
    /// peticion queda aprobada de inmediato, que es lo que hace util el flujo en pruebas.
    /// </summary>
    [Fact]
    public void CibaApruebaLaPeticionSiElLoginHintIdentificaAUnUsuario()
    {
        var started = _service.StartCiba(CibaRequest(loginHint: "jperez"));

        var pending = _store.Find(started.Value!.AuthReqId);

        Assert.True(pending.Succeeded);
        Assert.Equal("user-1", pending.Value!.Subject);
    }

    /// <summary>
    /// CIBA 7.1: el <c>login_hint</c> identifica a la persona a autenticar. Sin el, el mock no sabe a
    /// quien aprobar, asi que lo rechaza en vez de devolver un handle que no podria resolver.
    /// </summary>
    [Fact]
    public void CibaSinLoginHintEsInvalidRequest()
    {
        var started = _service.StartCiba(CibaRequest(loginHint: null));

        Assert.False(started.Succeeded);
        Assert.Equal("invalid_request", started.Error?.Code);
    }

    [Fact]
    public void CibaConUnLoginHintDesconocidoQuedaPendienteDeAprobacion()
    {
        var started = _service.StartCiba(CibaRequest(loginHint: "nadie-esta-en-el-mock"));

        Assert.True(started.Succeeded, started.Error?.ToString());
        var pending = _store.Find(started.Value!.AuthReqId);
        Assert.True(pending.Succeeded);
        Assert.Null(pending.Value!.Subject);
    }

    /// <summary>
    /// El discovery anuncia <c>backchannel_user_code_parameter_supported</c>: el user code solo se
    /// devuelve si el cliente declara que sabe mostrarlo.
    /// </summary>
    [Fact]
    public void CibaDevuelveUserCodeSoloSiElClienteLoPide()
    {
        var withCode = _service.StartCiba(CibaRequest(loginHint: "jperez", wantsUserCode: true));
        var withoutCode = _service.StartCiba(CibaRequest(loginHint: "jperez", wantsUserCode: false));

        Assert.False(string.IsNullOrWhiteSpace(withCode.Value!.UserCode));
        Assert.Equal(VerificationUri.Constant, withCode.Value.VerificationUri);
        Assert.Null(withoutCode.Value!.UserCode);
        Assert.Null(withoutCode.Value.VerificationUri);
    }

    [Fact]
    public void CibaCaducaPasadosSusCincoMinutos()
    {
        var started = _service.StartCiba(CibaRequest(loginHint: "jperez"));

        _clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        Assert.False(_store.Find(started.Value!.AuthReqId).Succeeded);
    }

    [Fact]
    public void CibaTambienRechazaSecretoIncorrecto()
    {
        var started = _service.StartCiba(new CibaAuthorizationStart(
            ConfidentialCredentials("no-es-el-secreto"),
            "jperez",
            "openid",
            WantsUserCode: false));

        Assert.False(started.Succeeded);
        Assert.Equal("invalid_client", started.Error?.Code);
    }

    /// <summary>
    /// Usuario de pruebas. Va duplicado en vez de heredado de <c>GrantHandlerTestBase</c> porque esa base
    /// es de otra jerarquia y arrastraria su clave de firma y sus stores a una prueba que solo necesita
    /// un <c>IUserStore</c> para que CIBA apruebe una peticion.
    /// </summary>
    private static User SampleUser() => new(
        "user-1",
        "jperez",
        "clave",
        new Dictionary<string, System.Text.Json.JsonElement>());

    private static ClientCredentials SpaCredentials() =>
        new(ClientAuthenticationMethods.ClientSecretPost, ClientStoreFixture.SpaClientId, null);

    private static ClientCredentials ConfidentialCredentials(string secret) =>
        new(
            ClientAuthenticationMethods.ClientSecretPost,
            ClientStoreFixture.ConfidentialWebClientId,
            secret);

    private static DeviceAuthorizationStart SpaDevice() => new(SpaCredentials(), "openid email");

    private static CibaAuthorizationStart CibaRequest(string? loginHint, bool wantsUserCode = false) =>
        new(SpaCredentials(), loginHint, "openid", wantsUserCode);
}