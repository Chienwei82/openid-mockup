using System.Net;
using System.Text.Json;
using OidcMock.Core.Authorization;
using OidcMock.Core.Discovery;
using OidcMock.Core.Grants;
using OidcMock.Host.Endpoints;
using static OidcMock.IntegrationTests.Endpoints.OidcTestClient;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// Comportamiento observable de GET y POST /connect/endsession: redireccion al
/// post_logout_redirect_uri registrado, rechazo de los que no lo estan, cierre real de la sesion del
/// navegador, pagina de "sesion cerrada" y aviso de frontchannel logout.
/// </summary>
public sealed class EndSessionEndpointTests
{
    private const string PostLogoutRedirectUri = "https://localhost:5173/";
    private const string ClosedSessionHeading = "Sesi&#243;n cerrada";
    private const string HttpGet = "GET";
    private const string HttpPost = "POST";

    [Theory]
    [InlineData(HttpGet)]
    [InlineData(HttpPost)]
    public async Task ConPostLogoutRedirectUriRegistradoRedirigeConState(string method)
    {
        using var client = Create();

        using var response = await EndSessionAsync(client, method, new Dictionary<string, string>
        {
            ["id_token_hint"] = await SignInAndGetIdTokenAsync(client),
            ["client_id"] = ClientId,
            ["post_logout_redirect_uri"] = PostLogoutRedirectUri,
            ["state"] = "st-2"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location!.ToString();
        Assert.StartsWith(PostLogoutRedirectUri, location, StringComparison.Ordinal);
        Assert.Contains("state=st-2", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnPostLogoutRedirectUriNoRegistradoSeRechazaConInvalidRequest()
    {
        using var client = Create();

        using var response = await EndSessionAsync(client, HttpGet, new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["post_logout_redirect_uri"] = "https://atacante.example/"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_request", await ReadErrorAsync(response));
    }

    [Fact]
    public async Task UnIdTokenHintInvalidoSeRechazaConInvalidRequest()
    {
        using var client = Create();

        using var response = await EndSessionAsync(client, HttpGet, new Dictionary<string, string>
        {
            ["id_token_hint"] = "esto-no-es-un-jwt",
            ["client_id"] = ClientId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_request", await ReadErrorAsync(response));
    }

    [Fact]
    public async Task UnClientIdDesconocidoSeRechazaConInvalidRequest()
    {
        using var client = Create();

        using var response = await EndSessionAsync(client, HttpGet, new Dictionary<string, string>
        {
            ["client_id"] = "cliente-inventado"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SinPostLogoutRedirectUriMuestraLaPaginaDeSesionCerrada()
    {
        using var client = Create();

        using var response = await EndSessionAsync(client, HttpGet, new Dictionary<string, string>
        {
            ["client_id"] = ClientId
        });

        var html = await ReadBodyAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/html", response.Content.Headers.ContentType?.MediaType ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(ClosedSessionHeading, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ElEndSessionCierraLaSesionDelNavegador()
    {
        using var client = Create();
        var idToken = await SignInAndGetIdTokenAsync(client);

        using var signInAgain = await client.GetAsync(
            AuthorizeUrl(PromptValues.None),
            TestContext.Current.CancellationToken);
        Assert.Contains("code=", signInAgain.Headers.Location!.ToString(), StringComparison.Ordinal);

        using var response = await EndSessionAsync(client, HttpGet, new Dictionary<string, string>
        {
            ["id_token_hint"] = idToken,
            ["client_id"] = ClientId,
            ["post_logout_redirect_uri"] = PostLogoutRedirectUri
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var afterLogout = await client.GetAsync(
            AuthorizeUrl(PromptValues.None),
            TestContext.Current.CancellationToken);

        Assert.Contains("error=login_required", afterLogout.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LaPaginaDeCierreIncluyeElIframeDeFrontchannelLogoutConIssYSid()
    {
        using var client = Create();
        await SignInAsync(client);

        using var response = await EndSessionAsync(client, HttpGet, new Dictionary<string, string>
        {
            ["client_id"] = ClientId
        });

        var html = await ReadBodyAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<iframe", html, StringComparison.Ordinal);
        Assert.Contains("iss=", html, StringComparison.Ordinal);
        Assert.Contains("sid=", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnClienteSinFrontchannelLogoutUriNoIncluyeIframe()
    {
        using var client = Create();

        using var response = await EndSessionAsync(client, HttpGet, new Dictionary<string, string>
        {
            ["client_id"] = ServiceClientId
        });

        Assert.DoesNotContain("<iframe", await ReadBodyAsync(response), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ElEndSessionBorraLaCookieDeSesion()
    {
        using var client = Create();
        await SignInAsync(client);

        using var response = await EndSessionAsync(client, HttpGet, new Dictionary<string, string>
        {
            ["client_id"] = ClientId
        });

        var cookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(AuthSessionCookie.Name + "=", StringComparison.Ordinal));

        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    private static Task<HttpResponseMessage> EndSessionAsync(
        HttpClient client,
        string method,
        Dictionary<string, string> values) =>
        method == HttpGet
            ? client.GetAsync(
                $"{PathBase}/{EndpointPaths.EndSession}?{Query(values)}",
                TestContext.Current.CancellationToken)
            : PostFormAsync(client, EndpointPaths.EndSession, values);

    private static string Query(IEnumerable<KeyValuePair<string, string>> values) =>
        string.Join('&', values.Select(value => $"{value.Key}={Uri.EscapeDataString(value.Value)}"));

    /// <summary>
    /// Recorre login y canje hasta quedarse con un id_token real del cliente, que es lo que un cliente
    /// envia como id_token_hint al cerrar sesion.
    /// </summary>
    private static async Task<string> SignInAndGetIdTokenAsync(HttpClient client)
    {
        var tokens = await RequestTokensAsync(client, GrantTypes.AuthorizationCode, await SignInAsync(client));

        return tokens.IdToken ?? throw new InvalidOperationException("El token endpoint no devolvio id_token.");
    }

    private static string AuthorizeUrl(string prompt)
    {
        var parameters = DefaultParameters();
        parameters["prompt"] = prompt;

        return BuildAuthorizeUrl(parameters);
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await ReadBodyAsync(response)).RootElement.GetProperty("error").GetString();
}
