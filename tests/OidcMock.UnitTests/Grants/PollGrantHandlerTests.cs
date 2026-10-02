using OidcMock.Core.Authorization;
using OidcMock.Core.Grants;
using OidcMock.Core.PendingRequests;
using OidcMock.UnitTests.Fixtures;
using OidcMock.UnitTests.Tokens;

namespace OidcMock.UnitTests.Grants;

/// <summary>
/// El otro lado del ciclo de sondeo: lo que el token endpoint responde mientras el cliente espera.
/// Device code y CIBA comparten la base, asi que se prueba una vez y el caso de CIBA confirma que
/// toma el mismo camino.
///
/// El ciclo de RFC 8628 3.5 es una maquina de estados y estos tests la fijan entera: pendiente, aprobado,
/// denegado, caducado y canjeado. Cada estado se alcanza moviendo el reloj inyectado.
/// </summary>
public sealed class PollGrantHandlerTests : GrantHandlerTestBase
{
    private readonly InMemoryPendingAuthorizationStore _pending = null!;
    private readonly DeviceCodeGrantHandler _deviceCodeGrant = null!;
    private readonly CibaGrantHandler _cibaGrant = null!;

    public PollGrantHandlerTests()
    {
        _pending = new InMemoryPendingAuthorizationStore(Clock);
        _deviceCodeGrant = new DeviceCodeGrantHandler(_pending, UserStore, RefreshTokens, Tokens, Clock);
        _cibaGrant = new CibaGrantHandler(_pending, UserStore, RefreshTokens, Tokens, Clock);
    }

    [Fact]
    public async Task SondearSinAprobacionRespondeAuthorizationPending()
    {
        var issued = Issue("dc-1");

        var result = await _deviceCodeGrant.HandleAsync(Poll(issued.Handle));

        Assert.False(result.Succeeded);
        Assert.Equal("authorization_pending", result.Error?.Code);
    }

    [Fact]
    public async Task TrasAprobarEmiteLosTokensDelUsuario()
    {
        var issued = Issue("dc-2");
        _pending.Approve(issued.Handle, "jperez", "user-1", Now);

        var result = await _deviceCodeGrant.HandleAsync(Poll(issued.Handle));

        Assert.True(result.Succeeded, result.Error?.ToString());
        Assert.False(string.IsNullOrWhiteSpace(result.Value!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.Value.IdToken));
        Assert.False(string.IsNullOrWhiteSpace(result.Value.RefreshToken));
    }

    /// <summary>
    /// RFC 8628 3.4: el device_code es de un solo uso. Canjearlo dos veces devolveria dos access tokens
    /// por una sola aprobacion del usuario.
    /// </summary>
    [Fact]
    public async Task ElHandleEsDeUnSoloUso()
    {
        var issued = Issue("dc-3");
        _pending.Approve(issued.Handle, "jperez", "user-1", Now);

        await _deviceCodeGrant.HandleAsync(Poll(issued.Handle));
        var second = await _deviceCodeGrant.HandleAsync(Poll(issued.Handle));

        Assert.False(second.Succeeded);
        Assert.Equal("invalid_grant", second.Error?.Code);
    }

    [Fact]
    public async Task SiElUsuarioDenegaRespondeAccessDenied()
    {
        var issued = Issue("dc-4");
        _pending.Deny(issued.Handle);

        var result = await _deviceCodeGrant.HandleAsync(Poll(issued.Handle));

        Assert.False(result.Succeeded);
        Assert.Equal("access_denied", result.Error?.Code);
    }

    /// <summary>
    /// Un handle caducado no es un handle pendiente: el cliente tiene que dejar de preguntar y pedir uno
    /// nuevo, asi que responde <c>invalid_grant</c> y no <c>authorization_pending</c>.
    /// </summary>
    [Fact]
    public async Task UnHandleCaducadoRespondeInvalidGrantYNoQuedaPendiente()
    {
        var issued = Issue("dc-5", lifetime: TimeSpan.FromMinutes(1));
        Clock.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(1));

        var result = await _deviceCodeGrant.HandleAsync(Poll(issued.Handle));

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_grant", result.Error?.Code);
    }

    [Fact]
    public async Task UnHandleDesconocidoRespondeInvalidGrant()
    {
        var result = await _deviceCodeGrant.HandleAsync(Poll("dc-inexistente"));

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_grant", result.Error?.Code);
    }

    /// <summary>
    /// El handle se emitio para un cliente concreto: aceptarlo desde otro entregaria a un cliente los
    /// tokens de la autorizacion de otro.
    /// </summary>
    [Fact]
    public async Task ElHandleDeOtroClienteNoSeCanjea()
    {
        var issued = Issue("dc-6", clientId: ClientStoreFixture.SpaClientId);
        _pending.Approve(issued.Handle, "jperez", "user-1", Now);

        var result = await _deviceCodeGrant.HandleAsync(
            Poll(issued.Handle, clientId: ClientStoreFixture.ConfidentialWebClientId));

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_grant", result.Error?.Code);
    }

    /// <summary>
    /// Si el usuario desaparece del store entre la aprobacion y el sondeo, el canje no puede inventar un
    /// subject: lo dice en vez de emitir tokens sin usuario.
    /// </summary>
    [Fact]
    public async Task SiElUsuarioYaNoExisteNoEmiteTokens()
    {
        var issued = Issue("dc-7");
        _pending.Approve(issued.Handle, "jperez", "usuario-borrado", Now);

        var result = await _deviceCodeGrant.HandleAsync(Poll(issued.Handle));

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_grant", result.Error?.Code);
    }

    [Fact]
    public async Task ElAccessTokenLevaElSubjectDelUsuarioQueAprobo()
    {
        var issued = Issue("dc-8");
        _pending.Approve(issued.Handle, "jperez", "user-1", Now);

        var result = await _deviceCodeGrant.HandleAsync(Poll(issued.Handle));

        var token = Validator.Validate(
            result.Value!.AccessToken,
            Issuer,
            [ClientStoreFixture.SpaClientId],
            Clock);
        Assert.True(token.IsValid, token.Exception?.Message);
        Assert.Equal("user-1", TokenTestValidator.ReadClaim(result.Value.AccessToken, "sub").GetString());
    }

    /// <summary>
    /// Los scopes concedidos son los de la peticion pendiente, no los que el cliente dice en el sondeo: si
    /// el sondeo pudiera ampliarlos, bastaria pedir mas en el token endpoint.
    /// </summary>
    [Fact]
    public async Task LosTokensCarganLosScopesDeLaPeticionPendiente()
    {
        var issued = Issue("dc-9", scopes: ["openid", "email"]);
        _pending.Approve(issued.Handle, "jperez", "user-1", Now);

        var result = await _deviceCodeGrant.HandleAsync(Poll(issued.Handle, scopes: ["openid", "email", "roles"]));

        Assert.True(result.Succeeded, result.Error?.ToString());
        Assert.Equal("openid email", result.Value!.Scope);
    }

    /// <summary>
    /// CIBA recorre exactamente el mismo ciclo con el <c>auth_req_id</c> en lugar del
    /// <c>device_code</c>: la base es compartida y esto confirma que no se ha colado una excepcion.
    /// </summary>
    [Fact]
    public async Task CibaComparteElCicloDeSondeo()
    {
        var issued = Issue("ciba-1");
        var pending = await _cibaGrant.HandleAsync(Poll(issued.Handle));

        Assert.Equal("authorization_pending", pending.Error?.Code);

        _pending.Approve(issued.Handle, "jperez", "user-1", Now);
        var redeemed = await _cibaGrant.HandleAsync(Poll(issued.Handle));

        Assert.True(redeemed.Succeeded, redeemed.Error?.ToString());
    }

    [Fact]
    public async Task SinHandleElCanjeFalla()
    {
        var result = await _deviceCodeGrant.HandleAsync(Poll("dc-y", withHandle: false));

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_grant", result.Error?.Code);
    }

    private PendingAuthorizationRequest Issue(
        string handle,
        string clientId = ClientStoreFixture.SpaClientId,
        IReadOnlyList<string>? scopes = null,
        TimeSpan? lifetime = null) =>
        _pending.Issue(new PendingAuthorizationRequest(
            handle,
            clientId,
            scopes ?? ["openid", "email"],
            AuthorizationOf(clientId),
            Now + (lifetime ?? TimeSpan.FromMinutes(10)),
            TimeSpan.FromSeconds(5)));

    private ValidatedAuthorizationRequest AuthorizationOf(string clientId) =>
        new(
            Clients.Find(clientId)!,
            RedirectUri,
            ["openid", "email"],
            "code",
            "query",
            Nonce: null,
            State: null,
            CodeChallenge: null,
            CodeChallengeMethod: null,
            Prompt: null);

    private TokenRequest Poll(
        string handle,
        string clientId = ClientStoreFixture.SpaClientId,
        IReadOnlyList<string>? scopes = null,
        bool withHandle = true) =>
        new(
            Clients.Find(clientId)!,
            Issuer,
            scopes ?? ["openid", "email"],
            Code: null,
            RedirectUri: null,
            CodeVerifier: null,
            RefreshToken: null,
            UserName: null,
            Password: null,
            DeviceCode: withHandle ? handle : null);
}