using Microsoft.Extensions.Time.Testing;
using OidcMock.Core.Clients;
using OidcMock.Core.Authorization;
using OidcMock.Core.Configuration;
using OidcMock.Core.EndSession;
using OidcMock.Core.Errors;
using OidcMock.Core.Tokens;
using OidcMock.UnitTests.Fixtures;

namespace OidcMock.UnitTests.EndSession;

/// <summary>
/// Reglas de /connect/endsession: que post_logout_redirect_uri se acepta, que un id_token_hint
/// invalido se rechaza, que la sesion del navegador queda cerrada y cuando se anuncia el
/// frontchannel logout del cliente.
/// </summary>
public sealed class EndSessionServiceTests
{
    private const string Issuer = "http://localhost:5000/personafisica/";
    private const string RegisteredRedirectUri = "http://localhost:5173/";
    private const string FrontchannelUri = "https://localhost:5173/frontchannel-logout";
    private const string ClientWithFrontchannel = "web-app-con-frontchannel";
    private const string OtherClientRedirectUri = "http://localhost:5173/otro-cliente";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryAuthSessionStore _sessions;
    private readonly StubIdTokenReader _idTokens = new();
    private readonly IClientStore _clients = new InMemoryClientStore([Spa(), SpaConFrontchannel(), OtroCliente()]);

    public EndSessionServiceTests() =>
        _sessions = new InMemoryAuthSessionStore(_time, new OidcMockOptions());

    [Fact]
    public void SinIdTokenHintNiRedirectMuestraLaPaginaDeCierre()
    {
        var result = EndSession();

        Assert.True(result.Succeeded);
        Assert.Null(result.Value!.RedirectUri);
    }

    [Fact]
    public void UnPostLogoutRedirectUriRegistradoSeAcepta()
    {
        var result = EndSession(postLogoutRedirectUri: RegisteredRedirectUri);

        Assert.True(result.Succeeded);
        Assert.Equal(RegisteredRedirectUri, result.Value!.RedirectUri);
    }

    [Fact]
    public void ElStateViajaAlPostLogoutRedirectUri()
    {
        var result = EndSession(postLogoutRedirectUri: RegisteredRedirectUri, state: "st-2");

        Assert.Equal("st-2", result.Value!.State);
    }

    [Fact]
    public void UnPostLogoutRedirectUriNoRegistradoSeRechaza()
    {
        var result = EndSession(postLogoutRedirectUri: "https://atacante.example/");

        Assert.Equal("invalid_request", result.Error?.Code);
        Assert.Equal(400, result.Error?.StatusCode);
    }

    [Fact]
    public void UnIdTokenHintInvalidoSeRechaza()
    {
        _idTokens.Fail();

        var result = EndSession(idTokenHint: "no-es-un-jwt");

        Assert.Equal("invalid_request", result.Error?.Code);
    }

    [Fact]
    public void UnPostLogoutRedirectUriRegistradoParaOtroClienteSeRechaza()
    {
        var result = EndSession(
            postLogoutRedirectUri: OtherClientRedirectUri,
            clientId: ClientStoreFixture.SpaClientId);

        Assert.Equal("invalid_request", result.Error?.Code);
    }

    [Fact]
    public void ElIdTokenHintDaElClienteConQueSeValidaElRedirect()
    {
        _idTokens.Return("jperez", ClientStoreFixture.ServiceClientId);

        var result = EndSession(idTokenHint: "hint", postLogoutRedirectUri: RegisteredRedirectUri);

        Assert.Equal("invalid_request", result.Error?.Code);
    }

    [Fact]
    public void UnClientIdDesconocidoSeRechaza()
    {
        var result = EndSession(clientId: "cliente-inventado");

        Assert.Equal("invalid_request", result.Error?.Code);
    }

    [Fact]
    public void LaSesionDelNavegadorQuedaCerrada()
    {
        var sessionId = _sessions.Start("jperez", "user-1").SessionId;

        EndSession(sessionId: sessionId);

        Assert.Null(_sessions.Find(sessionId));
    }

    [Fact]
    public void SinSesionNoHayQueCerrarNadaYNoFalla()
    {
        var result = EndSession(sessionId: "sesion-inexistente");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void SoloUnClienteConFrontchannelLogoutUriLoAnuncia()
    {
        Assert.Null(EndSession().Value!.FrontchannelLogoutUri);
        Assert.Equal(FrontchannelUri, EndSession(clientId: ClientWithFrontchannel).Value!.FrontchannelLogoutUri);
    }

    [Fact]
    public void LaNotificacionDeFrontchannelGuardaElIssuerYSesion()
    {
        var sessionId = _sessions.Start("jperez", "user-1").SessionId;

        var result = EndSession(clientId: ClientWithFrontchannel, sessionId: sessionId);

        Assert.Equal(Issuer, result.Value!.Issuer);
        Assert.Equal(sessionId, result.Value.SessionId);
    }

    private Result<EndSessionResult> EndSession(
        string? idTokenHint = null,
        string? postLogoutRedirectUri = null,
        string? state = null,
        string? clientId = null,
        string? sessionId = null) =>
        new EndSessionService(_clients, _sessions, _idTokens).EndSession(new EndSessionRequest(
            Issuer,
            idTokenHint,
            postLogoutRedirectUri,
            state,
            clientId ?? (idTokenHint is null ? null : ClientStoreFixture.SpaClientId),
            sessionId));

    private static Client Spa() => ClientStoreFixture.Spa();

    private static Client SpaConFrontchannel() =>
        Spa() with { ClientId = ClientWithFrontchannel, FrontchannelLogoutUri = FrontchannelUri };

    private static Client OtroCliente() =>
        Spa() with { ClientId = "otro-cliente", PostLogoutRedirectUris = [OtherClientRedirectUri] };

    /// <summary>
    /// Lector de id_token controlado por el test: decide si el hint es valido y a que cliente y sujeto
    /// corresponde, sin montar una clave de firma ni leer el JWKS.
    /// </summary>
    private sealed class StubIdTokenReader : IIdTokenReader
    {
        private IdTokenClaims? _claims;

        public void Return(string subject, string clientId, string? sessionId = null) =>
            _claims = new IdTokenClaims(subject, clientId, sessionId);

        public void Fail() => _claims = null;

        public Result<IdTokenClaims> Read(string idToken, string issuer) =>
            _claims is null
                ? Result<IdTokenClaims>.Fail(ProtocolErrors.InvalidRequest("El id_token_hint no es valido."))
                : Result<IdTokenClaims>.Ok(_claims);
    }
}