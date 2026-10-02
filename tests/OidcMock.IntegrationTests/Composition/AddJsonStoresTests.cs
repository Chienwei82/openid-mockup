using Microsoft.Extensions.DependencyInjection;
using OidcMock.Core.Clients;
using OidcMock.Core.Configuration;
using OidcMock.Core.Scopes;
using OidcMock.Core.Users;
using OidcMock.Host;

namespace OidcMock.IntegrationTests.Composition;

public sealed class AddJsonStoresTests
{
    [Fact]
    public void RegistraLosTresStoresResolublesPorSuInterfaz()
    {
        var services = BuildServices(RepositoryLayout.ConfigDirectory);

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IClientStore>());
        Assert.NotNull(provider.GetRequiredService<IUserStore>());
        Assert.NotNull(provider.GetRequiredService<IScopeStore>());
    }

    [Fact]
    public void LosStoresRegistradosLeenLaConfiguracionDeEjemplo()
    {
        var services = BuildServices(RepositoryLayout.ConfigDirectory);

        using var provider = services.BuildServiceProvider();

        var clientStore = provider.GetRequiredService<IClientStore>();
        var userStore = provider.GetRequiredService<IUserStore>();
        var scopeStore = provider.GetRequiredService<IScopeStore>();

        Assert.Contains(clientStore.List(), client => client.ClientId == "web-app-spa");
        Assert.Equal(3, userStore.List().Count);
        Assert.NotNull(scopeStore.Find("openid"));
    }

    [Fact]
    public void FallaAlArrancarConUnErrorClaroSiElJsonEstaMalFormado()
    {
        using var directory = new TempConfigDirectory();
        directory.WriteClientsFile("{ nope }");
        var services = BuildServices(directory.Path);

        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IConfigurationValidator>();

        var exception = Assert.Throws<ConfigurationException>(validator.Validate);
        Assert.Contains("clients.json", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidaUnaConfiguracionCorrectaSinLanzar()
    {
        var services = BuildServices(RepositoryLayout.ConfigDirectory);

        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IConfigurationValidator>();

        validator.Validate();
    }

    private static ServiceCollection BuildServices(string configDirectory)
    {
        var services = new ServiceCollection();
        services.AddJsonStores(new JsonStoreOptions { ConfigDirectory = configDirectory, ReloadOnChange = true });
        return services;
    }
}
