using OidcMock.Core.Clients;
using OidcMock.UnitTests.Fixtures;

namespace OidcMock.UnitTests.Clients;

/// <summary>
/// Cliente mas autenticacion: el metodo con el que se presento y el secreto que trajo.
/// El token endpoint no sabe de donde salio cada valor, solo que metodo se uso.
/// </summary>
public sealed class ClientAuthenticatorTests
{
    private static ClientAuthenticator AuthenticatorOver(IClientStore clientStore) =>
        ClientStoreFixture.AuthenticatorOver(clientStore);

    private readonly ClientAuthenticator _authenticator = AuthenticatorOver(ClientStoreFixture.Create());

    [Fact]
    public void ClientSecretBasicAceptaElSecretoCorrecto()
    {
        var client = _authenticator.Authenticate(Credentials(
            ClientAuthenticationMethods.ClientSecretBasic,
            ClientStoreFixture.ServiceClientId,
            "super-secret-backend"));

        Assert.Equal(ClientStoreFixture.ServiceClientId, client?.ClientId);
    }

    [Fact]
    public void ClientSecretBasicRechazaElSecretoIncorrecto()
    {
        var client = _authenticator.Authenticate(Credentials(
            ClientAuthenticationMethods.ClientSecretBasic,
            ClientStoreFixture.ServiceClientId,
            "secreto-malo"));

        Assert.Null(client);
    }

    [Fact]
    public void ClientSecretPostAceptaElSecretoCorrecto()
    {
        var client = _authenticator.Authenticate(Credentials(
            ClientAuthenticationMethods.ClientSecretPost,
            ClientStoreFixture.ServiceClientId,
            "super-secret-backend"));

        Assert.Equal(ClientStoreFixture.ServiceClientId, client?.ClientId);
    }

    [Fact]
    public void ClientSecretPostRechazaElSecretoIncorrecto()
    {
        var client = _authenticator.Authenticate(Credentials(
            ClientAuthenticationMethods.ClientSecretPost,
            ClientStoreFixture.ServiceClientId,
            "secreto-malo"));

        Assert.Null(client);
    }

    [Fact]
    public void RechazaUnClienteDesconocido()
    {
        var client = _authenticator.Authenticate(Credentials(
            ClientAuthenticationMethods.ClientSecretPost,
            "cliente-fantasma",
            "lo-que-sea"));

        Assert.Null(client);
    }

    [Fact]
    public void RechazaUnaPeticionSinClientId()
    {
        var client = _authenticator.Authenticate(Credentials(
            ClientAuthenticationMethods.ClientSecretPost,
            clientId: null,
            "lo-que-sea"));

        Assert.Null(client);
    }

    [Fact]
    public void UnClientePublicoSeIdentificaSoloPorClientId()
    {
        var client = _authenticator.Authenticate(Credentials(
            ClientAuthenticationMethods.ClientSecretPost,
            ClientStoreFixture.SpaClientId,
            clientSecret: null));

        Assert.Equal(ClientStoreFixture.SpaClientId, client?.ClientId);
    }

    [Fact]
    public void UnClientePublicoSeAutenticaAunqueLeSobreUnSecreto()
    {
        var client = _authenticator.Authenticate(Credentials(
            ClientAuthenticationMethods.ClientSecretPost,
            ClientStoreFixture.SpaClientId,
            "secreto-que-el-cliente-no-tiene"));

        Assert.Equal(ClientStoreFixture.SpaClientId, client?.ClientId);
    }

    [Fact]
    public void UnClienteConfidencialSinSecretoNoSeAutentica()
    {
        var client = _authenticator.Authenticate(Credentials(
            ClientAuthenticationMethods.ClientSecretBasic,
            ClientStoreFixture.ServiceClientId,
            clientSecret: null));

        Assert.Null(client);
    }

    [Fact]
    public void RechazaUnMetodoDeAutenticacionNoRegistrado()
    {
        var client = _authenticator.Authenticate(Credentials(
            "private_key_jwt",
            ClientStoreFixture.ServiceClientId,
            "super-secret-backend"));

        Assert.Null(client);
    }

    [Fact]
    public void AnunciaLosMetodosRegistradosParaElDescubrimiento()
    {
        var methods = AuthenticatorOver(ClientStoreFixture.Create()).SupportedMethods;

        Assert.Contains(ClientAuthenticationMethods.ClientSecretBasic, methods);
        Assert.Contains(ClientAuthenticationMethods.ClientSecretPost, methods);
        Assert.DoesNotContain("private_key_jwt", methods);
    }

    private static ClientCredentials Credentials(
        string method,
        string? clientId,
        string? clientSecret) => new(method, clientId, clientSecret);
}