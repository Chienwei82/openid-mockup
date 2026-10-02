using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OidcMock.Core.Configuration;
using OidcMock.Host;

namespace OidcMock.IntegrationTests.Composition;

public sealed class HostStartupTests
{
    private const string DiscoveryPath = "/personafisica/.well-known/openid-configuration";
    private const string UnknownPath = "/personafisica/connect/ruta-que-no-existe";

    [Fact]
    public async Task ArrancaConLaConfiguracionDeEjemploDelRepositorio()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var discoveryResponse = await client.GetAsync(DiscoveryPath, TestContext.Current.CancellationToken);
        using var unknownResponse = await client.GetAsync(UnknownPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, discoveryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
    }

    [Fact]
    public void FallaAlArrancarSiLaConfiguracionEstaMalFormada()
    {
        using var directory = new TempConfigDirectory();
        directory.WriteClientsFile("{ nope }");
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting(HostConfigDirectory.ConfigDirectorySettingName, directory.Path));

        var exception = Assert.Throws<ConfigurationException>(factory.CreateClient);

        Assert.Contains("clients.json", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("OidcMock:PathBase", "sin-barra-inicial")]
    [InlineData("OidcMock:Issuer", "no-es-una-url")]
    [InlineData("OidcMock:SessionLifetime", "00:00:00")]
    [InlineData("OidcMock:Serving:HttpsPort", "0")]
    public void NoArrancaConOpcionesInvalidas(string setting, string value)
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting(setting, value));

        var exception = Assert.Throws<OptionsValidationException>(factory.CreateClient);

        Assert.Contains(setting, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NoArrancaConUnOrigenCorsQueNoEsUnOrigen()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("OidcMock:AllowedCorsOrigins:0", "*"));

        var exception = Assert.Throws<OptionsValidationException>(factory.CreateClient);

        Assert.Contains("OidcMock:AllowedCorsOrigins:0", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ArrancaConHttpPlanoSinHttpsParaContenedores()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder
                .UseSetting("OidcMock:Serving:UseHttps", "false")
                .UseSetting("OidcMock:Serving:AllowHttp", "true"));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(DiscoveryPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Regresion: la configuracion de escucha vive en OidcMock:Serving, y leer la seccion OidcMock
    /// entera devolvia las opciones por defecto, asi que el contenedor acababa sirviendo HTTPS con el
    /// certificado de desarrollo en lugar de HTTP plano.
    /// </summary>
    [Fact]
    public void LaConfiguracionDeEscuchaSeLeeDeSuPropiaSeccion()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{HostConfigDirectory.SectionName}:{ServingOptions.SectionName}:UseHttps"] = "false",
                [$"{HostConfigDirectory.SectionName}:{ServingOptions.SectionName}:AllowHttp"] = "true",
                [$"{HostConfigDirectory.SectionName}:{ServingOptions.SectionName}:HttpPort"] = "8099"
            })
            .Build();

        var serving = configuration
            .GetSection($"{HostConfigDirectory.SectionName}:{ServingOptions.SectionName}")
            .Get<ServingOptions>();

        Assert.NotNull(serving);
        Assert.False(serving.UseHttps);
        Assert.True(serving.AllowHttp);
        Assert.Equal(8099, serving.HttpPort);
    }

    [Fact]
    public async Task LosOrigenesPorDefectoSeAplicanCuandoLaConfiguracionNoTraeNinguno()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(Request(origin: OidcMockOptions.DefaultAllowedCorsOrigins[1]), TestContext.Current.CancellationToken);

        Assert.Equal(OidcMockOptions.DefaultAllowedCorsOrigins[1], AllowOrigin(response));
    }

    [Fact]
    public async Task UnOrigenConfiguradoReemplazaALosOrigenesPorDefecto()
    {
        const string origin = "http://localhost:9999";
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("OidcMock:AllowedCorsOrigins:0", origin));
        using var client = factory.CreateClient();

        using var allowed = await client.SendAsync(Request(origin), TestContext.Current.CancellationToken);
        using var denied = await client.SendAsync(
            Request(OidcMockOptions.DefaultAllowedCorsOrigins[0]),
            TestContext.Current.CancellationToken);

        Assert.Equal(origin, AllowOrigin(allowed));
        Assert.Null(AllowOrigin(denied));
    }

    private static HttpRequestMessage Request(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, DiscoveryPath);
        request.Headers.Add("Origin", origin);
        return request;
    }

    private static string? AllowOrigin(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values)
            ? Assert.Single(values)
            : null;
}
