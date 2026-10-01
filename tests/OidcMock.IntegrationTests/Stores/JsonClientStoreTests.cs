using OidcMock.Core.Configuration;
using OidcMock.Host.Stores;

namespace OidcMock.IntegrationTests.Stores;

public sealed class JsonClientStoreTests
{
    private const string SampleClientId = "web-app-spa";
    private const string UnknownClientId = "no-existe";
    private const string ClientsFileName = "clients.json";

    [Fact]
    public void CargaElClienteConfiguradoConTodosSusDatos()
    {
        var store = CreateStoreOverExampleConfig();
        var client = store.Find(SampleClientId);

        Assert.NotNull(client);
        Assert.Equal(SampleClientId, client.ClientId);
        Assert.Null(client.ClientSecret);
        Assert.Contains("http://localhost:5173/callback", client.RedirectUris);
        Assert.Contains("http://localhost:5173/", client.PostLogoutRedirectUris);
        Assert.Equal(["authorization_code", "refresh_token"], client.AllowedGrantTypes);
        Assert.Contains("openid", client.AllowedScopes);
        Assert.True(client.RequirePkce);
        Assert.False(client.RequireClientSecret);
        Assert.Equal(TimeSpan.FromMinutes(30), client.TokenLifetimes.AccessToken);
        Assert.Equal(TimeSpan.FromMinutes(30), client.TokenLifetimes.IdentityToken);
        Assert.Equal(TimeSpan.FromHours(8), client.TokenLifetimes.RefreshToken);
        Assert.Equal(TimeSpan.FromMinutes(5), client.TokenLifetimes.AuthorizationCode);
    }

    [Fact]
    public void ExponeElBloqueDeBranding()
    {
        var store = CreateStoreOverExampleConfig();
        var client = store.Find(SampleClientId);

        Assert.NotNull(client);
        Assert.Equal("OidcMock - Persona Física", client.Branding.DisplayName);
        Assert.Equal("/assets/logo-mock.svg", client.Branding.LogoUrl);
        Assert.Equal("#00695C", client.Branding.PrimaryColor);
    }

    [Fact]
    public void DevuelveNullParaUnClienteInexistente()
    {
        var store = CreateStoreOverExampleConfig();

        Assert.Null(store.Find(UnknownClientId));
    }

    [Fact]
    public void CargaLosDosClientesDeEjemplo()
    {
        var store = CreateStoreOverExampleConfig();

        Assert.Equal(2, store.List().Count);
    }

    [Fact]
    public void LanzaUnErrorClaroSiElJsonEstaMalFormado()
    {
        using var directory = new TempConfigDirectory();
        directory.WriteClientsFile("{ \"clients\": [ ");

        var store = new JsonClientStore(directory.Path);

        var exception = Assert.Throws<ConfigurationException>(() => store.Find(SampleClientId));
        Assert.Contains("clients.json", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void LanzaUnErrorClaroSiFaltaElArchivoDeConfiguracion()
    {
        using var directory = new TempConfigDirectory();
        var store = new JsonClientStore(directory.Path);

        var exception = Assert.Throws<ConfigurationException>(() => store.Find(SampleClientId));
        Assert.Contains("clients.json", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RecargaElArchivoCuandoEsteCambiaEnCaliente()
    {
        using var directory = new TempConfigDirectory();
        directory.WriteClientsFile(ClientsJson(SampleClientId, brandColor: "#111111"));
        var store = new JsonClientStore(directory.Path, reloadOnChange: true);

        Assert.Equal("#111111", store.Find(SampleClientId)!.Branding.PrimaryColor);

        directory.WriteClientsFile(ClientsJson(SampleClientId, brandColor: "#222222"));

        Assert.Equal("#222222", store.Find(SampleClientId)!.Branding.PrimaryColor);
    }

    [Fact]
    public void MantieneLaCacheCuandoReloadEstaDesactivado()
    {
        using var directory = new TempConfigDirectory();
        directory.WriteClientsFile(ClientsJson(SampleClientId, brandColor: "#111111"));
        var store = new JsonClientStore(directory.Path, reloadOnChange: false);

        Assert.Equal("#111111", store.Find(SampleClientId)!.Branding.PrimaryColor);

        directory.WriteClientsFile(ClientsJson(SampleClientId, brandColor: "#222222"));

        Assert.Equal("#111111", store.Find(SampleClientId)!.Branding.PrimaryColor);
    }

    [Fact]
    public void DetectaElCambioAunqueElArchivoMantengaElSelloDeDisco()
    {
        using var directory = new TempConfigDirectory();
        var filePath = directory.WriteClientsFile(ClientsJson(SampleClientId, brandColor: "#111111"));
        var store = new JsonClientStore(directory.Path, reloadOnChange: true);

        Assert.Equal("#111111", store.Find(SampleClientId)!.Branding.PrimaryColor);

        var originalWriteTimeUtc = File.GetLastWriteTimeUtc(filePath);
        directory.WriteClientsFile(ClientsJson(SampleClientId, brandColor: "#222222"));
        File.SetLastWriteTimeUtc(filePath, originalWriteTimeUtc);

        Assert.Equal("#222222", store.Find(SampleClientId)!.Branding.PrimaryColor);
    }

    [Fact]
    public void ReutilizaElDominioCacheadoEnConsultasSuccessivas()
    {
        using var directory = new TempConfigDirectory();
        directory.WriteClientsFile(ClientsJson(SampleClientId, brandColor: "#111111"));
        var store = new JsonClientStore(directory.Path, reloadOnChange: true);

        var firstRead = store.List();
        var secondRead = store.List();

        Assert.Same(firstRead, secondRead);
        Assert.Same(store.Find(SampleClientId), store.Find(SampleClientId));
    }

    [Fact]
    public void SirveLaCacheSiElArchivoDesapareceYLaRecargaEstaDesactivada()
    {
        using var directory = new TempConfigDirectory();
        directory.WriteClientsFile(ClientsJson(SampleClientId, brandColor: "#111111"));
        var store = new JsonClientStore(directory.Path, reloadOnChange: false);

        Assert.Equal("#111111", store.Find(SampleClientId)!.Branding.PrimaryColor);

        File.Delete(directory.FilePath(ClientsFileName));

        Assert.Equal("#111111", store.Find(SampleClientId)!.Branding.PrimaryColor);
    }

    [Fact]
    public void LanzaUnErrorClaroSiFaltaElClientId()
    {
        using var directory = new TempConfigDirectory();
        directory.WriteClientsFile("""
        {
          "clients": [
            { "client_id": "", "client_secret": "x" }
          ]
        }
        """);

        var store = new JsonClientStore(directory.Path);

        var exception = Assert.Throws<ConfigurationException>(() => store.Find(SampleClientId));
        Assert.Contains("client_id", exception.Message, StringComparison.Ordinal);
    }

    private static JsonClientStore CreateStoreOverExampleConfig() => new(RepositoryLayout.ConfigDirectory);

    private static string ClientsJson(string clientId, string brandColor) => $$"""
    {
      "clients": [
        {
          "client_id": "{{clientId}}",
          "client_secret": "secreto",
          "redirect_uris": [ "http://localhost/cb" ],
          "post_logout_redirect_uris": [],
          "allowed_grant_types": [ "authorization_code" ],
          "allowed_scopes": [ "openid" ],
          "require_pkce": true,
          "require_client_secret": true,
          "token_lifetimes": {
            "access_token": "00:10:00",
            "id_token": "00:10:00",
            "refresh_token": "01:00:00",
            "authorization_code": "00:01:00"
          },
          "branding": { "display_name": "Demo", "logo_url": null, "primary_color": "{{brandColor}}" }
        }
      ]
    }
    """;
}
