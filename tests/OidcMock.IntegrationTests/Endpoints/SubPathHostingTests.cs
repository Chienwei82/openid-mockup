using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OidcMock.Core.Discovery;
using OidcMock.Host;
using static OidcMock.IntegrationTests.Endpoints.OidcTestClient;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// Hosting bajo una ruta relativa, como cuando se publica en IIS en https://miserver/oidc: el mock
/// sirve todos sus endpoints bajo la base, anuncia el issuer configurado y las URLs que pinta (el
/// logo, por ejemplo) llevan el prefijo. La base ajena sigue dando 404.
/// </summary>
public sealed class SubPathHostingTests
{
    private const string BaseUrl = "https://miserver/oidc";
    private const string BasePath = "/oidc";
    private const string DiscoveryPath = "/.well-known/openid-configuration";
    private const string LogoPath = "/assets/logo-mock.svg";

    [Fact]
    public async Task ElDiscoverySeSirveBajoLaBaseYAnunciaElIssuerConfigurado()
    {
        using var factory = FactoryForBaseUrl();
        using var client = factory.CreateClient();

        var document = await GetJsonAsync(client, $"{BasePath}{DiscoveryPath}");

        Assert.Equal(
            $"{BaseUrl}/",
            document.RootElement.GetProperty("issuer").GetString());
        Assert.StartsWith(
            $"{BaseUrl}/",
            document.RootElement.GetProperty("token_endpoint").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task LaBaseAjenaSigueDando404()
    {
        using var factory = FactoryForBaseUrl();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{PathBase}{DiscoveryPath}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ElLogoDelLoginSeSirveBajoLaBase()
    {
        using var factory = FactoryForBaseUrl();
        using var client = factory.CreateClient();

        using var loginPage = await client.GetAsync(
            $"{BasePath}/{EndpointPaths.Authorize}?{AuthorizeQuery()}",
            TestContext.Current.CancellationToken);
        var html = await loginPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains($"{BasePath}{LogoPath}", html, StringComparison.Ordinal);

        using var logo = await client.GetAsync($"{BasePath}{LogoPath}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, logo.StatusCode);
    }

    private static WebApplicationFactory<Program> FactoryForBaseUrl() =>
        MockHost.Create().WithWebHostBuilder(
            builder => builder.UseSetting(HostConfigDirectory.BaseUrlSettingName, BaseUrl));

    private static string AuthorizeQuery() =>
        string.Join(
            '&',
            DefaultParameters().Select(parameter =>
                $"{parameter.Key}={Uri.EscapeDataString(parameter.Value ?? string.Empty)}"));

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
