using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using OidcMock.ClientCompatibilityTests.Infrastructure;

namespace OidcMock.ClientCompatibilityTests;

/// <summary>
/// Renovacion de la sesion y cierre de sesion, los dos caminos que se recorren con el handler
/// OpenIdConnect de ASP.NET Core contra el mock.
///
/// El handler no canjea el refresh token por su cuenta: lo guarda y es la aplicacion quien lo usa
/// (aqui <c>/refresh</c>), y para el logout usa el <c>end_session_endpoint</c> del discovery con el
/// <c>id_token_hint</c> que hay en la cookie.
/// </summary>
public sealed class RefreshAndLogoutTests
{
    [Fact]
    public async Task ElRefreshEmiteTokensNuevosYMantieneLaSesion()
    {
        await using var signedIn = await SignedInWorldAsync();

        using var response = await RefreshAsync(signedIn);

        Assert.Equal(CompatibilityWorld.UserSubject, Read(signedIn, response, "sub"));
        Assert.False(string.IsNullOrEmpty(Read(signedIn, response, "access_token")));
        Assert.False(string.IsNullOrEmpty(Read(signedIn, response, "refresh_token")));
    }

    [Fact]
    public async Task ElAccessTokenRenovadoSigueSirviendoParaElUserInfo()
    {
        await using var signedIn = await SignedInWorldAsync();

        using var response = await RefreshAsync(signedIn);

        using var userInfo = await signedIn.World.Mock.CreateApiClient().SendAsync(
            WithBearer(Read(signedIn, response, "access_token")),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, userInfo.StatusCode);
    }

    [Fact]
    public async Task ElRefreshTokenCaducaAlCanjearse()
    {
        await using var signedIn = await SignedInWorldAsync();

        // La primera renovacion rota el token: el canjeado se guarda aparte para poder reenviarlo.
        using var _first = await RefreshAsync(signedIn);

        // Reenviar el token ya canjeado tiene que ser rechazado. Si no, un token capturado serviria
        // para renovar la sesion indefinidamente.
        using var reused = await signedIn.Browser.GetAsync(
            $"{signedIn.World.ClientBaseAddress}/refresh?anterior=1",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
    }

    [Fact]
    public async Task ElLogoutCierraLaSesionEnElCliente()
    {
        await using var signedIn = await SignedInWorldAsync();

        await LogoutAsync(signedIn);
        using var closed = await signedIn.Browser.GetAsync(
            $"{signedIn.World.ClientBaseAddress}/",
            TestContext.Current.CancellationToken);

        // El logout vuelve del end session al cliente, que ya no tiene cookie.
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        Assert.Equal("sesion cerrada", await closed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        using var profile = await signedIn.Browser.GetAsync(
            $"{signedIn.World.ClientBaseAddress}/profile",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, profile.StatusCode);
    }

    [Fact]
    public async Task ElLogoutCierraLaSesionDelMock()
    {
        await using var signedIn = await SignedInWorldAsync();

        await LogoutAsync(signedIn);

        // Sin sesion en el mock, un authorize con prompt=none ya no puede conceder en silencio y
        // responde login_required por el redirect_uri (OAuth lleva los errores de protocolo ahi).
        var configuration = await signedIn.World.ReadConfigurationAsync();
        using var client = signedIn.World.Mock.CreateApiClient();

        using var silent = await client.GetAsync(
            $"{configuration.AuthorizationEndpoint}?client_id={CompatibilityWorld.ClientId}" +
            $"&redirect_uri={Uri.EscapeDataString($"{signedIn.World.ClientBaseAddress}/signin-oidc")}" +
            $"&response_type=code&scope=openid&prompt=none" +
            $"&code_challenge={PkceChallenge}&code_challenge_method=S256&state=st",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, silent.StatusCode);
        Assert.Contains("error=login_required", silent.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Levanta el mundo, entra y devuelve el navegador con la cookie: lo que sigue tiene que seguir en
    /// la <b>misma</b> sesion que hizo el login. Si cada prueba abriera el suyo, el refresh y el
    /// logout se estarian probando contra un cliente sin sesion, y el fallo seria del arnes y no del
    /// mock.
    /// </summary>
    private static async Task<SignedInWorld> SignedInWorldAsync()
    {
        var world = await CompatibilityWorld.StartAsync();
        await world.StartClientAsync();

        var browser = new BrowserSession();
        using var _login = await browser.SignInAsync(
            world.ClientBaseAddress,
            CompatibilityWorld.UserName,
            TestContext.Current.CancellationToken);

        return new SignedInWorld(world, browser);
    }

    private static Task<HttpResponseMessage> RefreshAsync(SignedInWorld signedIn) =>
        signedIn.Browser.GetAsync(
            $"{signedIn.World.ClientBaseAddress}/refresh",
            TestContext.Current.CancellationToken);

    private static async Task LogoutAsync(SignedInWorld signedIn) =>
        await signedIn.Browser.GetAsync(
            $"{signedIn.World.ClientBaseAddress}/logout",
            TestContext.Current.CancellationToken);

    /// <summary>
    /// Lee un campo de la respuesta de la renovacion, comprobando antes que la renovacion haya
    /// funcionado: si el token endpoint devolvio un error, el campo no existe y la asercion real
    /// seria sobre un null.
    /// </summary>
    private static string? Read(SignedInWorld signedIn, HttpResponseMessage response, string field)
    {
        var payload = response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult();

        Assert.True(
            HttpStatusCode.OK == response.StatusCode,
            $"La renovacion fallo: {payload}{Environment.NewLine}{CollectingLoggerProvider.Dump()}");

        return JsonDocument.Parse(payload).RootElement.GetProperty(field).GetString();
    }

    private static HttpRequestMessage WithBearer(string? accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, MockEndpoints.Resolve(MockEndpoints.UserInfo));

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return request;
    }

    /// <summary>Challenge valido para PKCE; el valor no importa, el metodo si.</summary>
    private const string PkceChallenge = "prueba-de-compatibilidad-codigo-de-un-solo-uso-0123456789";
}

/// <summary>
/// El mundo de una prueba junto al navegador que entro en el. Se devuelven juntos porque la sesion
/// vive en las cookies del navegador: separarlos haria que cada prueba arrancara sin sesion.
/// </summary>
public sealed class SignedInWorld : IAsyncDisposable
{
    public SignedInWorld(CompatibilityWorld world, BrowserSession browser)
    {
        World = world;
        Browser = browser;
    }

    public CompatibilityWorld World { get; }

    public BrowserSession Browser { get; }

    public async ValueTask DisposeAsync()
    {
        Browser.Dispose();
        await World.DisposeAsync();
    }
}
