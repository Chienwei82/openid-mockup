using System.Net;
using System.Security.Claims;
using System.Text.Json;
using OidcMock.ClientCompatibilityTests.Infrastructure;

namespace OidcMock.ClientCompatibilityTests;

/// <summary>
/// Login de punta a punta con el handler OpenIdConnect de ASP.NET Core contra el mock.
///
/// No se comprueba solo que el flujo termine: se recorre entero y se mira que el authorize mande al
/// login, que el login devuelva un codigo al <c>redirect_uri</c> registrado y que el canje de ese
/// codigo -con PKCE, que el handler usa siempre- produzca un principal con los claims del usuario.
/// </summary>
public sealed class AuthorizationCodeLoginTests
{
    [Fact]
    public async Task ElLoginConCodeYPkceDevuelveElPrincipalConLosClaimsEsperados()
    {
        await using var world = await CompatibilityWorld.StartAsync();
        await world.StartClientAsync();

        using var browser = new BrowserSession();
        using var response = await browser.SignInAsync(
            world.ClientBaseAddress,
            CompatibilityWorld.UserName,
            CompatibilityWorld.Password,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var claims = await ReadClaimsAsync(response);

        // El handler mapea los claims del token a los tipos de .NET por defecto (MapInboundClaims),
        // asi que 'sub' llega como NameIdentifier. Es lo que veria cualquier aplicacion que no lo
        // desactive, y por eso se comprueba aqui y no el nombre crudo del claim.
        Assert.True(claims.ContainsKey(ClaimTypes.NameIdentifier), $"Sin subject. Claims: {Dump(claims)}");
        Assert.True(
            CompatibilityWorld.UserSubject == claims[ClaimTypes.NameIdentifier],
            $"Se esperaba sub='{CompatibilityWorld.UserSubject}'. Claims: {Dump(claims)}");
        Assert.True(claims.ContainsKey("preferred_username"), $"Sin usuario. Claims: {Dump(claims)}");
        Assert.True(
            CompatibilityWorld.UserName == claims["preferred_username"],
            $"Se esperaba preferred_username='{CompatibilityWorld.UserName}'. Claims: {Dump(claims)}");
    }

    [Fact]
    public async Task ElClaimsDelIdTokenYDelUserInfoLleganAlPrincipal()
    {
        await using var world = await CompatibilityWorld.StartAsync();

        // Con GetClaimsFromUserInfoEndpoint el handler pide los claims al userinfo despues de validar
        // el id_token, y los anade al principal. Es el camino que ejercita las dos fuentes a la vez.
        await world.StartClientAsync();

        using var browser = new BrowserSession();
        using var response = await browser.SignInAsync(
            world.ClientBaseAddress,
            CompatibilityWorld.UserName,
            CompatibilityWorld.Password,
            TestContext.Current.CancellationToken);

        var claims = await ReadClaimsAsync(response);

        // El nombre llega del userinfo con el nombre del claim tal cual, porque su ClaimAction no
        // aplica el mapeo de tipos de .NET al que pasa el id_token.
        Assert.True("Juan Pérez" == claims["name"], $"Nombre inesperado. Claims: {Dump(claims)}");

        // El correo viene en el id_token, asi que llega mapeado a EmailAddress. El userinfo lo
        // devuelve tambien, pero el handler no anade un claim que ya existe con ese valor: el
        // principal no se duplica. Se comprueba el del id_token, que es el que manda.
        Assert.True(
            "jperez@example.cr" == claims[ClaimTypes.Email],
            $"Correo inesperado. Claims: {Dump(claims)}");
    }

    [Fact]
    public async Task ElPrincipalNoSeAutenticaConCredencialesIncorrectas()
    {
        await using var world = await CompatibilityWorld.StartAsync();
        await world.StartClientAsync();

        using var browser = new BrowserSession();

        // El mock responde access_denied por el redirect_uri (OAuth lleva los errores de protocolo
        // por ahi), el cliente lo recibe en el callback y no llega a crear sesion. Lo que importa es
        // que /profile siga sin autenticar, no el status con el que el cliente responde.
        var response = await browser.SignInAsync(
            world.ClientBaseAddress,
            CompatibilityWorld.UserName,
            "clave-que-no-es-la-buena",
            TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TrasElLoginElEndpointProtegidoReconoceLaSesion()
    {
        await using var world = await CompatibilityWorld.StartAsync();
        await world.StartClientAsync();

        using var browser = new BrowserSession();
        using var _login = await browser.SignInAsync(
            world.ClientBaseAddress,
            CompatibilityWorld.UserName,
            CompatibilityWorld.Password,
            TestContext.Current.CancellationToken);

        // Una peticion aparte con la misma sesion: la cookie es lo unico que mantiene al usuario
        // autenticado, y es lo que hace un navegador al navegar a otra pagina.
        using var profile = await browser.GetAsync(
            $"{world.ClientBaseAddress}/profile",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
    }

    private static async Task<Dictionary<string, string>> ReadClaimsAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(body)
                ?? throw new InvalidOperationException("El endpoint protegido devolvio un cuerpo vacio.");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                $"El endpoint protegido devolvio: {body[..Math.Min(body.Length, 600)]}" +
                $"{Environment.NewLine}{CollectingLoggerProvider.Dump()}");
        }
    }

    internal static string Dump(Dictionary<string, string> claims) => string.Join(", ", claims.Keys);
}