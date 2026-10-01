using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OidcMock.Host;

namespace OidcMock.IntegrationTests.Endpoints;

public sealed class DiscoveryEndpointTests
{
    private const string DefaultPathBase = "/personafisica";
    private const string CustomPathBase = "/otro-prefijo";
    private const string ConfigurationPath = "/.well-known/openid-configuration";
    private const string JwksPath = "/.well-known/openid-configuration/jwks";
    private const string SigningKeyFileName = "signing-key.pem";
    private const string PathBaseSettingName = HostConfigDirectory.PathBaseSettingName;
    private const string IssuerSettingName = HostConfigDirectory.IssuerSettingName;

    [Fact]
    public async Task PublicaElDiscoveryConCodigoOkYContentTypeJson()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{DefaultPathBase}{ConfigurationPath}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ElDiscoveryAnunciaElIssuerDelHostQueSeConsulta()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var document = await GetDiscoveryAsync(client, DefaultPathBase);

        Assert.Equal(
            $"{client.BaseAddress!.Scheme}://{client.BaseAddress.Authority}{DefaultPathBase}/",
            document.RootElement.GetProperty("issuer").GetString());
    }

    [Fact]
    public async Task TodosLosEndpointsDelDiscoveryCuelganDelIssuer()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var document = await GetDiscoveryAsync(client, DefaultPathBase);
        var issuer = document.RootElement.GetProperty("issuer").GetString()!;

        var urls = document.RootElement
            .EnumerateObject()
            .Where(property => property.Value.ValueKind == JsonValueKind.String)
            .Select(property => property.Value.GetString()!)
            .ToArray();

        Assert.NotEmpty(urls);
        Assert.All(urls, url => Assert.StartsWith(issuer, url, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ElJwksPublicaUnaSolaClaveRsaConSuKid()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var key = (await GetJsonAsync(client, $"{DefaultPathBase}{JwksPath}")).RootElement.GetProperty("keys").EnumerateArray().Single();

        Assert.Equal("RSA", key.GetProperty("kty").GetString());
        Assert.Equal("sig", key.GetProperty("use").GetString());
        Assert.Equal("RS256", key.GetProperty("alg").GetString());
        Assert.False(string.IsNullOrWhiteSpace(key.GetProperty("kid").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(key.GetProperty("n").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(key.GetProperty("e").GetString()));
    }

    [Fact]
    public async Task LaClaveSePersisteEnElConfigEnLaPrimeraPeticion()
    {
        using var directory = new TempConfigDirectory();
        CopyExampleConfig(directory);
        var keyFile = directory.FilePath(SigningKeyFileName);
        using var factory = CreateFactory(directory);
        using var client = factory.CreateClient();

        Assert.False(File.Exists(keyFile));

        using var response = await client.GetAsync($"{DefaultPathBase}{JwksPath}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(File.Exists(keyFile));
    }

    [Fact]
    public async Task ElKidNoCambiaEntreReiniciosSiElArchivoPemExiste()
    {
        using var directory = new TempConfigDirectory();
        CopyExampleConfig(directory);

        var firstKid = await ReadKidAsync(directory);
        var secondKid = await ReadKidAsync(directory);

        Assert.Equal(firstKid, secondKid);
    }

    [Fact]
    public async Task CambiarElPathBaseCambiaLaRutaYTodasLasUrls()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting(PathBaseSettingName, CustomPathBase));
        using var client = factory.CreateClient();

        var document = await GetDiscoveryAsync(client, CustomPathBase);
        var issuer = document.RootElement.GetProperty("issuer").GetString()!;

        Assert.Equal($"{client.BaseAddress!.Scheme}://{client.BaseAddress.Authority}{CustomPathBase}/", issuer);
        Assert.Equal($"{issuer}connect/token", document.RootElement.GetProperty("token_endpoint").GetString());
        Assert.Equal($"{issuer}.well-known/openid-configuration/jwks", document.RootElement.GetProperty("jwks_uri").GetString());

        using var defaultPathBaseResponse = await client.GetAsync($"{DefaultPathBase}{ConfigurationPath}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, defaultPathBaseResponse.StatusCode);
    }

    [Fact]
    public async Task ElIssuerConfiguradoGanaAlHostDeLaPeticion()
    {
        const string issuer = "https://oauth2.bccr.fi.cr/personafisica";
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting(IssuerSettingName, issuer));
        using var client = factory.CreateClient();

        var document = await GetDiscoveryAsync(client, DefaultPathBase);

        Assert.Equal($"{issuer}/", document.RootElement.GetProperty("issuer").GetString());
    }

    private static async Task<string> ReadKidAsync(TempConfigDirectory directory)
    {
        using var factory = CreateFactory(directory);
        using var client = factory.CreateClient();

        return (await GetJsonAsync(client, $"{DefaultPathBase}{JwksPath}"))
            .RootElement.GetProperty("keys")[0].GetProperty("kid").GetString()!;
    }

    private static WebApplicationFactory<Program> CreateFactory(TempConfigDirectory directory) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting(HostConfigDirectory.ConfigDirectorySettingName, directory.Path));

    private static async Task<JsonDocument> GetDiscoveryAsync(HttpClient client, string pathBase) =>
        await GetJsonAsync(client, $"{pathBase}{ConfigurationPath}");

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static void CopyExampleConfig(TempConfigDirectory directory)
    {
        foreach (var fileName in new[] { "clients.json", "users.json", "scopes.json" })
        {
            directory.WriteFile(fileName, File.ReadAllText(RepositoryLayout.ConfigFile(fileName)));
        }
    }
}
