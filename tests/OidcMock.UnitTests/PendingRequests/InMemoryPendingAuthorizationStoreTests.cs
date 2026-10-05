using Microsoft.Extensions.Time.Testing;
using OidcMock.Core.Authorization;
using OidcMock.Core.Clients;
using OidcMock.Core.PendingRequests;

namespace OidcMock.UnitTests.PendingRequests;

public sealed class InMemoryPendingAuthorizationStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly FakeTimeProvider _clock = new(Now);
    private readonly InMemoryPendingAuthorizationStore _store = null!;

    public InMemoryPendingAuthorizationStoreTests() => _store = new InMemoryPendingAuthorizationStore(_clock);

    [Fact]
    public void EmiteUnaPeticionQueSePuedeConsultar()
    {
        var issued = Issue();

        Assert.True(_store.Find(issued.Handle).Succeeded);
    }

    [Fact]
    public void UnaPeticionPendienteNoSePuedeCanjear()
    {
        var issued = Issue();

        Assert.False(_store.Redeem(issued.Handle).Succeeded);
    }

    [Fact]
    public void TrasAprobarSePuedeCanjear()
    {
        var issued = Issue();
        _store.Approve(issued.Handle, "jperez", "user-1", Now);

        var redeemed = _store.Redeem(issued.Handle);

        Assert.True(redeemed.Succeeded);
        Assert.Equal("user-1", redeemed.Value?.Subject);
    }

    [Fact]
    public void ElCanjedeEsDeUnSoloUso()
    {
        var issued = Issue();
        _store.Approve(issued.Handle, "jperez", "user-1", Now);

        Assert.True(_store.Redeem(issued.Handle).Succeeded);
        Assert.False(_store.Redeem(issued.Handle).Succeeded);
    }

    [Fact]
    public void DenegarlaImpideElCanjede()
    {
        var issued = Issue();
        _store.Approve(issued.Handle, "jperez", "user-1", Now);
        _store.Deny(issued.Handle);

        Assert.False(_store.Redeem(issued.Handle).Succeeded);
    }

    [Fact]
    public void UnaPeticionCaducadaDesaparece()
    {
        Issue();

        _clock.Advance(Lifetime + TimeSpan.FromSeconds(1));

        Assert.Empty(_store.List());
    }

    /// El store distingue el handle caducado del desconocido, que es lo que permite al sondeo responder
    /// <c>expired_token</c> en vez de <c>invalid_grant</c>. Antes <see cref="Find"/> los trataba igual:
    /// el caducado se descartaba en el momento y no quedaba rastro para el grant.
    /// </summary>
    [Fact]
    public void PollDevuelveElCaducadoYLoDescarta()
    {
        Issue();
        _clock.Advance(Lifetime + TimeSpan.FromSeconds(1));

        var polled = _store.Poll("handle-1");

        Assert.True(polled.Succeeded);
        Assert.Empty(_store.List());
    }

    [Fact]
    public void PollDeUnHandleDesconocidoFallaConInvalidGrant()
    {
        var polled = _store.Poll("inventado");

        Assert.False(polled.Succeeded);
        Assert.Equal("invalid_grant", polled.Error?.Code);
    }

    /// El sondeo no consume la peticion: preguntar dos veces tiene que devolverla las dos veces.
    /// </summary>
    [Fact]
    public void PollNoConsumeLaPeticion()
    {
        Issue();

        Assert.True(_store.Poll("handle-1").Succeeded);
        Assert.True(_store.Poll("handle-1").Succeeded);
    }

    /// El intervalo se lleva por handle: el sondeo de uno deja al otro con su primer sondeo intacto.
    /// </summary>
    [Fact]
    public void ElUltimoSondeoSeRegistraPorHandle()
    {
        Issue();
        _store.Poll("handle-1");

        Assert.True(_store.Find("handle-1").Value!.PolledTooSoonAt(Now));
        Assert.False(_store.Find("handle-1").Value!.PolledTooSoonAt(Now + TimeSpan.FromSeconds(6)));
    }

    [Fact]
    public void UnHandleDesconocidoNoSeEncuentra()
    {
        Assert.False(_store.Find("inventado").Succeeded);
    }

    private PendingAuthorizationRequest Issue() =>
        _store.Issue(new PendingAuthorizationRequest(
            "handle-1",
            "web-app-spa",
            ["openid"],
            Authorization(),
            Now + Lifetime,
            TimeSpan.FromSeconds(5)));

    private static ValidatedAuthorizationRequest Authorization() =>
        new(
            new Client("web-app-spa", null, [], [], ["authorization_code"], ["openid"], true, false,
                new TokenLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30), TimeSpan.FromHours(8), Lifetime),
                new Branding("OidcMock", null, null)),
            "https://localhost:5173/callback",
            ["openid"],
            "code",
            "query",
            null,
            "st-1",
            null,
            null,
            null);
}