using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using OidcMock.ClientCompatibilityTests.Infrastructure;

namespace OidcMock.ClientCompatibilityTests;

/// <summary>
/// El mock anuncia <c>pushed_authorization_request_endpoint</c> en el discovery, asi que un cliente
/// real que honor ese anuncio empuja la peticion y llega al authorize con un <c>request_uri</c> en
/// lugar de los parametros.
///
/// Este archivo prueba ese camino a mano, sin cliente OIDC de por medio: es el test de regresion del
/// fallo que hizo fallar el login de punta a punta.
/// </summary>
public sealed class PushedAuthorizationTests
{
    private const string Accept = "accept";

    /// <summary>
    /// Lo que el handler OpenIdConnect hace cuando el discovery trae un endpoint de PAR, y lo que
    /// hace cualquier cliente RFC 9126: POST al endpoint con los parametros de la peticion mas
    /// <c>client_secret</c>, y luego GET al authorize con solo <c>client_id</c> y <c>request_uri</c>.
    /// </summary>
    [Fact]
    public async Task ElAuthorizeAceptaUnaPeticionEmpujadaPorRequestUri()
    {
        await using var world = await CompatibilityWorld.StartAsync();
        using var client = world.Mock.CreateApiClient();

        var requestUri = await PushAsync(client, world);

        using var response = await client.GetAsync(
            $"{world.Mock.Issuer}{MockEndpoints.Authorize}?client_id={CompatibilityWorld.ClientId}&request_uri={Uri.EscapeDataString(requestUri)}",
            TestContext.Current.CancellationToken);

        // Sin resolver el request_uri, el authorize no ve redirect_uri ni response_type y responde
        // invalid_request: el cliente se queda sin poder autenticarse.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"redirect_uri\"", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ElRequestUriNoSirveParaEmitirUnSegundoCodigo()
    {
        await using var world = await CompatibilityWorld.StartAsync();
        using var client = world.Mock.CreateApiClient();

        var requestUri = await PushAsync(client, world);

        // El login y el consentimiento ocurren con el mismo request_uri, asi que resolverlo no lo
        // consume: lo que lo caduca es emitir el codigo (RFC 9126 4).
        using var login = await client.GetAsync(AuthorizeUri(world, requestUri), TestContext.Current.CancellationToken);
        await ApproveAsync(client, world, await login.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        using var second = await client.GetAsync(
            AuthorizeUri(world, requestUri),
            TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.OK, second.StatusCode);
        var body = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("invalid_request_uri", body, StringComparison.Ordinal);
    }

    /// <summary>Responde la pantalla de identidad del mock con el perfil por defecto.</summary>
    private static async Task ApproveAsync(HttpClient client, CompatibilityWorld world, string html)
    {
        var fields = HiddenFields.Parse(html);

        fields["action"] = Accept;

        using var response = await client.PostAsync(
            $"{world.Mock.Issuer}{MockEndpoints.Authorize}",
            new FormUrlEncodedContent(fields),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static string AuthorizeUri(CompatibilityWorld world, string requestUri) =>
        $"{world.Mock.Issuer}{MockEndpoints.Authorize}" +
        $"?client_id={CompatibilityWorld.ClientId}&request_uri={Uri.EscapeDataString(requestUri)}";

    private static async Task<string> PushAsync(HttpClient client, CompatibilityWorld world)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = CompatibilityWorld.ClientId,
            ["client_secret"] = CompatibilityWorld.ClientSecret,
            ["response_type"] = "code",
            ["redirect_uri"] = RedirectUriOf(world),
            ["scope"] = string.Join(' ', CompatibilityWorld.DefaultScopes),
            ["state"] = "estado-de-prueba",
            ["nonce"] = "nonce-de-prueba",
            ["code_challenge"] = CodeChallenge,
            ["code_challenge_method"] = "S256"
        };

        using var response = await client.PostAsync(
            $"{world.Mock.Issuer}{MockEndpoints.PushedAuthorizationRequest}",
            new FormUrlEncodedContent(form),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return document.RootElement.GetProperty("request_uri").GetString()!;
    }

    private static string RedirectUriOf(CompatibilityWorld world) =>
        $"{world.ClientBaseAddress}/signin-oidc";

    private const string CodeChallenge = "prueba-de-compatibilidad-codigo-de-un-solo-uso-0123456789";
}