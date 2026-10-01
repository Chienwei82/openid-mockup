using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OidcMock.Core.Configuration;
using OidcMock.Host;

namespace OidcMock.IntegrationTests.Composition;

public sealed class HostStartupTests
{
    private const string UnknownPath = "/personafisica/.well-known/openid-configuration";

    [Fact]
    public async Task ArrancaConLaConfiguracionDeEjemploDelRepositorio()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(UnknownPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
}
