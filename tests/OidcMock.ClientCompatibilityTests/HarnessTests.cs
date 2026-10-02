using System.Net;
using System.Text.Json;
using OidcMock.ClientCompatibilityTests.Infrastructure;

namespace OidcMock.ClientCompatibilityTests;

/// <summary>
/// Tests del propio arnes de pruebas.
///
/// El resto de la suite usa el handler OpenIdConnect de verdad, pero para llegar a el usa una pieza
/// propia: un navegador que sigue redirecciones y formularios, un host de cliente y un renovador de
/// sesion. Si esa pieza va mal, los tests de login, refresh y logout pasan (o fallan) por el motivo
/// equivocado y el fallo real del mock nunca se ve. Estos tests comprueban que el arnes hace lo que
/// sus consumidores asumen.
/// </summary>
public sealed class HarnessTests
{
    [Fact]
    public async Task ElLoginDelArnesDejaUnaSesionAbiertaEnElCliente()
    {
        await using var world = await CompatibilityWorld.StartAsync();
        await world.StartClientAsync();

        using var browser = new BrowserSession();
        using var login = await browser.SignInAsync(
            world.ClientBaseAddress,
            CompatibilityWorld.UserName,
            CompatibilityWorld.Password,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        // El recurso protegido responde 200 solo si la cookie de sesion viaja: si el navegador no
        // guardara las cookies, los tests de refresh y logout estarian probando un cliente anonimo.
        using var profile = await browser.GetAsync(
            $"{world.ClientBaseAddress}/profile",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
    }

    [Fact]
    public async Task ElRenovadorEmiteTokensNuevosYLaSesionSigueAbierta()
    {
        await using var world = await CompatibilityWorld.StartAsync();
        await world.StartClientAsync();

        using var browser = new BrowserSession();
        using var _login = await browser.SignInAsync(
            world.ClientBaseAddress,
            CompatibilityWorld.UserName,
            CompatibilityWorld.Password,
            TestContext.Current.CancellationToken);

        using var renewed = await browser.GetAsync(
            $"{world.ClientBaseAddress}/refresh",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
        Assert.False(string.IsNullOrEmpty(Read(renewed, "access_token")));
        Assert.False(string.IsNullOrEmpty(Read(renewed, "refresh_token")));

        // La renovacion tiene que dejar la sesion utilizable: si el arnes no reemitiera la cookie,
        // los tests siguientes estarian navegando como anonimos.
        using var profile = await browser.GetAsync(
            $"{world.ClientBaseAddress}/profile",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
    }

    /// <summary>
    /// El token que el renovador guarda aparte es el que se acaba de canjear, y alcanza para volver a
    /// intentarlo. Si el arnes guardara cualquier otra cosa, la prueba de rotacion del mock pasaria sin
    /// comprobar la rotacion.
    /// </summary>
    [Fact]
    public async Task ElRenovadorGuardaElRefreshTokenCanjeado()
    {
        await using var world = await CompatibilityWorld.StartAsync();
        await world.StartClientAsync();

        using var browser = new BrowserSession();
        using var _login = await browser.SignInAsync(
            world.ClientBaseAddress,
            CompatibilityWorld.UserName,
            CompatibilityWorld.Password,
            TestContext.Current.CancellationToken);

        using var _first = await browser.GetAsync(
            $"{world.ClientBaseAddress}/refresh",
            TestContext.Current.CancellationToken);

        using var reused = await browser.GetAsync(
            $"{world.ClientBaseAddress}/refresh?anterior=1",
            TestContext.Current.CancellationToken);

        // El token guardado ya no vale, asi que el canje tiene que fallar. Un 200 aqui significaria que
        // el arnes reenvio algo que no era el token viejo, y la rotacion del mock no se estaria probando.
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
    }

    /// <summary>Sin sesion, el recurso protegido responde 401: el arnes no se autenticaria solo.</summary>
    [Fact]
    public async Task ElArnesNoInventaUnaSesionQueNoHay()
    {
        await using var world = await CompatibilityWorld.StartAsync();
        await world.StartClientAsync();

        using var browser = new BrowserSession();
        using var profile = await browser.GetAsync(
            $"{world.ClientBaseAddress}/profile",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, profile.StatusCode);
    }

    private static string? Read(HttpResponseMessage response, string field) =>
        JsonDocument
            .Parse(response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult())
            .RootElement.GetProperty(field).GetString();
}
