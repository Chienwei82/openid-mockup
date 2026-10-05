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

    /// El descubrimiento anuncia los dos metodos, asi que PAR, que tambien exige cliente autenticado,
    /// tiene que aceptarlos igual que el token endpoint. Antes solo leia el cuerpo, asi que un cliente
    /// de verdad, que manda <c>client_secret_basic</c> en el encabezado, seenia rechazado con
    /// <c>invalid_client</c> contra un metodo que el propio mock anuncia.
    /// </summary>
    [Fact]
    public async Task ParAceptaClientSecretBasicComoElTokenEndpoint()
    {
        using var client = Create();

        using var response = await PostWithBasicAuthAsync(
            client,
            EndpointPaths.PushedAuthorizationRequest,
            PushedRequestForm(),
            ConfidentialClientId,
            ConfidentialSecret);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("request_uri").GetString()));
    }

    [Fact]
    public async Task ParRechazaElSecretoIncorrectoConInvalidClient()
    {
        using var client = Create();

        using var response = await PostWithBasicAuthAsync(
            client,
            EndpointPaths.PushedAuthorizationRequest,
            PushedRequestForm(),
            ConfidentialClientId,
            "secreto-incorrecto");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_client", body.RootElement.GetProperty("error").GetString());
    }

    /// RFC 6749 2.3.1, igual que en el token endpoint: si hay encabezado Basic, el <c>client_secret</c>
    /// del cuerpo no autentica. Sin esta regla, un cuerpo con el secreto correcto se saltaria el Basic.
    /// </summary>
    [Fact]
    public async Task EnParElEncabezadoBasicGanaAlClientSecretDelCuerpo()
    {
        using var client = Create();

        var form = new Dictionary<string, string>(PushedRequestForm())
        {
            ["client_id"] = ConfidentialClientId,
            ["client_secret"] = ConfidentialSecret
        };

        using var response = await PostWithBasicAuthAsync(
            client,
            EndpointPaths.PushedAuthorizationRequest,
            form,
            ConfidentialClientId,
            "secreto-incorrecto");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_client", body.RootElement.GetProperty("error").GetString());
    }

    /// Un cliente publico se identifica solo por <c>client_id</c>, tambien en PAR: la proteccion la
    /// aporta el PKCE ya registrado, no un secreto que no tiene.
    /// </summary>
    [Fact]
    public async Task ParAceptaUnClientePublicoSinSecreto()
    {
        using var client = Create();

        var form = new Dictionary<string, string>(PushedRequestForm())
        {
            ["client_id"] = ClientId,
            ["redirect_uri"] = RedirectUri
        };

        using var response = await PostFormAsync(client, EndpointPaths.PushedAuthorizationRequest, form);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("request_uri").GetString()));
    }

    private static Dictionary<string, string> PushedRequestForm() =>
        new()
        {
            ["client_id"] = ConfidentialClientId,
            ["redirect_uri"] = RedirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid",
            ["code_challenge"] = CodeVerifier,
            ["code_challenge_method"] = "plain"
        };

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
}