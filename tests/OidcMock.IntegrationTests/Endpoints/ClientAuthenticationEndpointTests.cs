using System.Net;
using System.Text.Json;
using OidcMock.Core.Discovery;
using OidcMock.Core.Grants;
using static OidcMock.IntegrationTests.Endpoints.OidcTestClient;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// Autenticacion de cliente en el token endpoint: client_secret_basic (encabezado Authorization) y
/// client_secret_post (cuerpo del formulario), con la precedencia que fija RFC 6749 2.3.1.
/// </summary>
public sealed class ClientAuthenticationEndpointTests
{
    [Fact]
    public async Task ClientSecretBasicAutenticaConElEncabezadoAuthorization()
    {
        using var client = Create();

        using var response = await PostWithBasicAuthAsync(
            client,
            new Dictionary<string, string> { ["grant_type"] = GrantTypes.ClientCredentials },
            ServiceClientId,
            ServiceSecret);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("access_token").GetString()));
    }

    [Fact]
    public async Task ClientSecretBasicRechazaElSecretoIncorrecto()
    {
        using var client = Create();

        using var response = await PostWithBasicAuthAsync(
            client,
            new Dictionary<string, string> { ["grant_type"] = GrantTypes.ClientCredentials },
            ServiceClientId,
            "secreto-incorrecto");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_client", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ClientSecretPostRechazaElSecretoIncorrecto()
    {
        using var client = Create();

        using var response = await PostFormAsync(
            client,
            EndpointPaths.Token,
            new Dictionary<string, string>
            {
                ["grant_type"] = GrantTypes.ClientCredentials,
                ["client_id"] = ServiceClientId,
                ["client_secret"] = "secreto-incorrecto"
            });
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_client", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ElEncabezadoBasicGanaAlClientSecretDelCuerpo()
    {
        using var client = Create();

        // El cuerpo trae el secreto correcto, pero si hay encabezado Basic es el que cuenta.
        using var response = await PostWithBasicAuthAsync(
            client,
            new Dictionary<string, string>
            {
                ["grant_type"] = GrantTypes.ClientCredentials,
                ["client_id"] = ServiceClientId,
                ["client_secret"] = ServiceSecret
            },
            ServiceClientId,
            "secreto-incorrecto");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_client", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task UnClientePublicoSeAutenticaSoloPorClientId()
    {
        using var client = Create();
        var code = await SignInAsync(client);

        using var response = await PostTokenAsync(client, AuthorizationCodeForm(code));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ElDescubrimientoAnunciaLosDosMetodosDeAutenticacion()
    {
        using var client = Create();

        using var response = await client.GetAsync(
            $"{PathBase}/{EndpointPaths.Configuration}",
            TestContext.Current.CancellationToken);
        var body = await ReadJsonAsync(response);

        var methods = body.RootElement
            .GetProperty("token_endpoint_auth_methods_supported")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();

        Assert.Contains("client_secret_basic", methods);
        Assert.Contains("client_secret_post", methods);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
}