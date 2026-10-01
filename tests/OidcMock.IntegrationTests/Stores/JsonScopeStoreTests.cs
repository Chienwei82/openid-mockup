using OidcMock.Core.Configuration;
using OidcMock.Host.Stores;

namespace OidcMock.IntegrationTests.Stores;

public sealed class JsonScopeStoreTests
{
    private const string SampleScope = "email";
    private const string UnknownScope = "scope-inexistente";

    [Fact]
    public void CargaElScopeConfiguradoConSusClaims()
    {
        var store = new JsonScopeStore(RepositoryLayout.ConfigDirectory);
        var scope = store.Find(SampleScope);

        Assert.NotNull(scope);
        Assert.Equal(SampleScope, scope.Name);
        Assert.Equal(["email", "email_verified"], scope.Claims);
    }

    [Fact]
    public void DevuelveNullParaUnScopeInexistente()
    {
        var store = new JsonScopeStore(RepositoryLayout.ConfigDirectory);

        Assert.Null(store.Find(UnknownScope));
    }

    [Fact]
    public void CargaLosScopesDeEjemplo()
    {
        var store = new JsonScopeStore(RepositoryLayout.ConfigDirectory);

        Assert.Contains(store.List(), scope => scope.Name == "openid");
        Assert.Contains(store.List(), scope => scope.Name == "custom.profile");
    }

    [Fact]
    public void LanzaUnErrorClaroSiElJsonEstaMalFormado()
    {
        using var directory = new TempConfigDirectory();
        directory.WriteScopesFile("{ \"scopes\": {} }");

        var store = new JsonScopeStore(directory.Path);

        var exception = Assert.Throws<ConfigurationException>(() => store.Find(SampleScope));
        Assert.Contains("scopes.json", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RecargaElArchivoCuandoEsteCambiaEnCaliente()
    {
        using var directory = new TempConfigDirectory();
        directory.WriteScopesFile(ScopesJson("email", "email"));
        var store = new JsonScopeStore(directory.Path, reloadOnChange: true);

        Assert.Equal(["email"], store.Find("email")!.Claims);

        directory.WriteScopesFile(ScopesJson("email", "email_verified", "email"));

        Assert.Equal(["email_verified", "email"], store.Find("email")!.Claims);
    }

    private static string ScopesJson(string name, params string[] claims) => $$"""
    {
      "scopes": [
        { "name": "{{name}}", "claims": [ {{string.Join(", ", claims.Select(claim => $"\"{claim}\""))}} ] }
      ]
    }
    """;
}
