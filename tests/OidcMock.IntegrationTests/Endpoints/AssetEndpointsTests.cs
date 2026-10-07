using System.Net;
using static OidcMock.IntegrationTests.Endpoints.OidcTestClient;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// Los logos del branding se sirven desde el propio binario: sin este endpoint, el &lt;img&gt; de la
/// pantalla de login pide /assets/logo-mock.svg y recibe 404, sobre todo en la version publicada,
/// donde no hay ningun archivo junto al ejecutable. Se prueban la ruta raiz y la del PathBase.
/// </summary>
public sealed class AssetEndpointsTests
{
    [Theory]
    [InlineData("logo-mock.svg")]
    [InlineData("logo-service.svg")]
    public async Task ElLogoDelBrandingSeSirveComoSvg(string fileName)
    {
        using var client = Create();

        using var response = await client.GetAsync($"/assets/{fileName}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<svg", await ReadBodyAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("logo-mock.svg")]
    [InlineData("logo-service.svg")]
    public async Task ElLogoTambienSeSirveBajoElPathBase(string fileName)
    {
        using var client = Create();

        using var response = await client.GetAsync(
            $"{PathBase}/assets/{fileName}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnAssetDesconocidoDevuelveNotFound()
    {
        using var client = Create();

        using var response = await client.GetAsync(
            "/assets/inexistente.svg",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Task<string> ReadBodyAsync(HttpResponseMessage response) =>
        response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}