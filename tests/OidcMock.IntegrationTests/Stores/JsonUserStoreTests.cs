using OidcMock.Core.Configuration;
using OidcMock.Host.Stores;

namespace OidcMock.IntegrationTests.Stores;

public sealed class JsonUserStoreTests
{
    private const string SampleUserName = "jperez";
    private const string SampleSubject = "user-persona-fisica";
    private const string UnknownUserName = "no-existe";

    [Fact]
    public void CargaElUsuarioConfiguradoConTodosSusDatos()
    {
        var store = new JsonUserStore(RepositoryLayout.ConfigDirectory);
        var user = store.FindByUserName(SampleUserName);

        Assert.NotNull(user);
        Assert.Equal(SampleSubject, user.Subject);
        Assert.Equal(SampleUserName, user.UserName);
        Assert.Equal("Passw0rd!", user.Password);
    }

    [Fact]
    public void ExponeLosClaimsConNombresLibresIncluidosLosDeBccr()
    {
        var store = new JsonUserStore(RepositoryLayout.ConfigDirectory);
        var user = store.FindByUserName(SampleUserName);

        Assert.NotNull(user);
        Assert.Equal("Juan", user.GetClaim("given_name").GetString());
        Assert.Equal("jperez@example.cr", user.GetClaim("email").GetString());
        Assert.Equal("01", user.GetClaim("codTipoId").GetString());
        Assert.Equal("us-88213", user.GetClaim("Bccr.IdUsuario").GetString());
        Assert.Equal("1-23456789", user.GetClaim("documentofva").GetString());
    }

    [Fact]
    public void ExponeLosClaimsQueNoSonTextoComoJson()
    {
        var store = new JsonUserStore(RepositoryLayout.ConfigDirectory);
        var user = store.FindByUserName(SampleUserName);

        Assert.NotNull(user);
        Assert.True(user.GetClaim("email_verified").GetBoolean());
        Assert.Equal(1735689600L, user.GetClaim("updated_at").GetInt64());
    }

    [Fact]
    public void BuscaPorSub()
    {
        var store = new JsonUserStore(RepositoryLayout.ConfigDirectory);

        var user = store.FindBySubject(SampleSubject);

        Assert.NotNull(user);
        Assert.Equal(SampleUserName, user.UserName);
    }

    [Fact]
    public void DevuelveNullParaUnUsuarioInexistente()
    {
        var store = new JsonUserStore(RepositoryLayout.ConfigDirectory);

        Assert.Null(store.FindByUserName(UnknownUserName));
        Assert.Null(store.FindBySubject(UnknownUserName));
    }

    [Fact]
    public void CargaLosUsuariosDeEjemplo()
    {
        var store = new JsonUserStore(RepositoryLayout.ConfigDirectory);

        // Se fijan los usuarios concretos y no el numero: agregar uno de ejemplo no debe romper el
        // test, igual que los clientes de ConfigCoherenceTests.
        Assert.Contains(store.List(), user => user.UserName == "jperez");
        Assert.Contains(store.List(), user => user.UserName == "empresa-demo");
        Assert.Contains(store.List(), user => user.UserName == "prueba");
        Assert.Contains(store.List(), user => user.UserName == "admin");
    }

    [Fact]
    public void LanzaUnErrorClaroSiElJsonEstaMalFormado()
    {
        using var directory = new TempConfigDirectory();
        directory.WriteUsersFile("not-json");

        var store = new JsonUserStore(directory.Path);

        var exception = Assert.Throws<ConfigurationException>(() => store.FindByUserName(SampleUserName));
        Assert.Contains("users.json", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RecargaElArchivoCuandoEsteCambiaEnCaliente()
    {
        using var directory = new TempConfigDirectory();
        directory.WriteUsersFile(UsersJson("jperez", "Passw0rd!"));
        var store = new JsonUserStore(directory.Path, reloadOnChange: true);

        Assert.Equal("Passw0rd!", store.FindByUserName(SampleUserName)!.Password);

        directory.WriteUsersFile(UsersJson("jperez", "NuevoPassword1!"));

        Assert.Equal("NuevoPassword1!", store.FindByUserName(SampleUserName)!.Password);
    }

    [Fact]
    public void LanzaUnErrorClaroSiFaltaElArchivoDeConfiguracion()
    {
        using var directory = new TempConfigDirectory();

        var store = new JsonUserStore(directory.Path);

        var exception = Assert.Throws<ConfigurationException>(() => store.FindByUserName(SampleUserName));
        Assert.Contains("users.json", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MantieneLaCacheCuandoReloadEstaDesactivado()
    {
        using var directory = new TempConfigDirectory();
        directory.WriteUsersFile(UsersJson("jperez", "Passw0rd!"));
        var store = new JsonUserStore(directory.Path, reloadOnChange: false);

        Assert.Equal("Passw0rd!", store.FindByUserName(SampleUserName)!.Password);

        directory.WriteUsersFile(UsersJson("jperez", "NuevoPassword1!"));

        Assert.Equal("Passw0rd!", store.FindByUserName(SampleUserName)!.Password);
    }

    private static string UsersJson(string userName, string password) => $$"""
    {
      "users": [
        {
          "sub": "{{userName}}",
          "username": "{{userName}}",
          "password": "{{password}}",
          "claims": { "given_name": "Juan" }
        }
      ]
    }
    """;
}
