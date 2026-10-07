using System.Net;
using static OidcMock.IntegrationTests.Endpoints.OidcTestClient;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// La pantalla raiz del mock: en lugar de rechazar la conexion, lista los clientes configurados y,
/// para los que usan el flujo de authorize, ofrece generar la URL de connect/authorize/callback con
/// los valores precargados, mostrada en un campo de solo lectura facil de copiar.
/// </summary>
public sealed class HomePageEndpointTests
{
    private const string RootPath = "/";
    private const string DetailRoute = "/authorize-url";
    private const string CallbackFragment = "connect/authorize/callback?";
    private const string MaterialYouToken = "color-scheme: dark";
    private const string SpaClientId = "web-app-spa";
    private const string ServiceClientId = "backend-service";

    [Fact]
    public async Task LaRaizListaLosClientesConfiguradosDelMock()
    {
        using var client = Create();

        using var response = await client.GetAsync(RootPath, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(SpaClientId, html, StringComparison.Ordinal);
        Assert.Contains("web-app-confidencial", html, StringComparison.Ordinal);
        Assert.Contains(ServiceClientId, html, StringComparison.Ordinal);
        Assert.Contains("fb02079c-3143-49e6-a776-dd9b002388d2", html, StringComparison.Ordinal);
        Assert.Contains(MaterialYouToken, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LaRaizSoloOfreceGenerarUrlParaLosClientesConRedirectUri()
    {
        using var client = Create();

        using var response = await client.GetAsync(RootPath, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains($"{DetailRoute}?client={SpaClientId}", html, StringComparison.Ordinal);
        Assert.DoesNotContain($"{DetailRoute}?client={ServiceClientId}", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ElDetalleGeneraLaUrlDeAuthorizeConValoresPrecargados()
    {
        using var client = Create();

        using var response = await client.GetAsync($"{DetailRoute}?client={SpaClientId}", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(CallbackFragment, html, StringComparison.Ordinal);
        Assert.Contains($"client_id={SpaClientId}", html, StringComparison.Ordinal);
        Assert.Contains("response_type=code", html, StringComparison.Ordinal);
        Assert.Contains("scope=openid", html, StringComparison.Ordinal);
        Assert.Contains("code_challenge=", html, StringComparison.Ordinal);
        Assert.Contains("code_challenge_method=S256", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LaUrlGeneradaEsFacilDeCopiar()
    {
        using var client = Create();

        using var response = await client.GetAsync($"{DetailRoute}?client={SpaClientId}", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("<textarea", html, StringComparison.Ordinal);
        Assert.Contains("readonly", html, StringComparison.Ordinal);
        Assert.Contains("Copiar", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnClienteSinRedirectUriNoGeneraUrlDeAuthorize()
    {
        using var client = Create();

        using var response = await client.GetAsync($"{DetailRoute}?client={ServiceClientId}", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(CallbackFragment, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnClienteDesconocidoDevuelve404()
    {
        using var client = Create();

        using var response = await client.GetAsync($"{DetailRoute}?client=no-existe", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
