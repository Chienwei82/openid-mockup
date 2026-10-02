using Microsoft.Extensions.Time.Testing;
using OidcMock.Core.Authorization;
using OidcMock.Core.Configuration;

namespace OidcMock.UnitTests.Sessions;

/// <summary>
/// Estado de sesion del navegador: es lo que permite que prompt=none funcione sin volver a
/// pedir credenciales, y lo que hace que prompt=login sea un "olvidate de mi sesion".
/// </summary>
public sealed class InMemoryAuthSessionStoreTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void AbreUnaSesionParaUnUsuario()
    {
        var session = CreateStore().Start("jperez", "user-1");

        Assert.Equal("jperez", session.UserName);
        Assert.Equal("user-1", session.Subject);
        Assert.False(string.IsNullOrEmpty(session.SessionId));
    }

    [Fact]
    public void RecuperaUnaSesionPorSuIdentificador()
    {
        var store = CreateStore();
        var session = store.Start("jperez", "user-1");

        Assert.Equal("jperez", store.Find(session.SessionId)?.UserName);
    }

    [Fact]
    public void CadaSesionTieneUnIdentificadorPropio()
    {
        var store = CreateStore();

        Assert.NotEqual(store.Start("jperez", "user-1").SessionId, store.Start("empresa", "user-2").SessionId);
    }

    [Fact]
    public void UnaSesionDesconocidaNoSeEncuentra() =>
        Assert.Null(CreateStore().Find("sesion-que-no-existe"));

    [Fact]
    public void CerrarUnaSesionLaInvalida()
    {
        var store = CreateStore();
        var session = store.Start("jperez", "user-1");

        store.End(session.SessionId);

        Assert.Null(store.Find(session.SessionId));
    }

    [Fact]
    public void UnaSesionCaducadaNoSeEncuentraYNoSeDevuelve()
    {
        var store = CreateStore();
        var session = store.Start("jperez", "user-1");

        _time.Advance(OidcMockOptions.DefaultSessionLifetime + TimeSpan.FromSeconds(1));

        Assert.Null(store.Find(session.SessionId));
        Assert.Empty(store.List());
    }

    [Fact]
    public void UnaSesionVigenteNoCaduca()
    {
        var store = CreateStore();
        var session = store.Start("jperez", "user-1");

        _time.Advance(OidcMockOptions.DefaultSessionLifetime - TimeSpan.FromSeconds(1));

        Assert.NotNull(store.Find(session.SessionId));
    }

    [Fact]
    public void CaducarSesionesDescartaSoloLasVencidas()
    {
        var store = CreateStore();
        var vencida = store.Start("viejo", "user-1");
        var vigente = store.Start("nuevo", "user-2");

        _time.Advance(OidcMockOptions.DefaultSessionLifetime + TimeSpan.FromSeconds(1));
        store.Expire();
        var afterwards = store.Start("posterior", "user-3");

        Assert.Empty(store.List().Where(item => item.SessionId == vencida.SessionId));
        Assert.Contains(store.List(), item => item.SessionId == afterwards.SessionId);
        Assert.NotEqual(vigente.SessionId, afterwards.SessionId);
    }

    private InMemoryAuthSessionStore CreateStore() => new(_time);
}
