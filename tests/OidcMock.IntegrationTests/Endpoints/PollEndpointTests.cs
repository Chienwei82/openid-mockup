using System.Net;
using System.Text.Json;
using OidcMock.Core.Discovery;
using OidcMock.Core.Grants;
using static OidcMock.IntegrationTests.Endpoints.OidcTestClient;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// Endpoints de autorizacion por sondeo: PAR, device authorization, CIBA y check_session_iframe.
/// </summary>
public sealed class PollEndpointTests
{
    [Fact]
    public async Task ParDevuelveUnRequestUriYVigencia()
    {
        using var client = Create();

        using var response = await PostFormAsync(client, EndpointPaths.PushedAuthorizationRequest, ValidParRequest());
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("request_uri").GetString()));
        Assert.True(body.RootElement.GetProperty("expires_in").GetInt32() > 0);
    }

    [Fact]
    public async Task ParRechazaUnClienteConSecretoIncorrectoConInvalidClient()
    {
        using var client = Create();

        using var response = await PostFormAsync(
            client,
            EndpointPaths.PushedAuthorizationRequest,
            new Dictionary<string, string>
            {
                ["client_id"] = ServiceClientId,
                ["client_secret"] = "secreto-malo",
                ["redirect_uri"] = RedirectUri,
                ["response_type"] = "code",
                ["scope"] = "openid"
            });
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_client", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ParRechazaUnRedirectUriNoRegistrado()
    {
        using var client = Create();

        using var response = await PostFormAsync(
            client,
            EndpointPaths.PushedAuthorizationRequest,
            new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["redirect_uri"] = "https://atacante.example/callback",
                ["response_type"] = "code",
                ["scope"] = "openid"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeviceAuthorizationEmiteDeviceCodeYUserCode()
    {
        using var client = Create();

        using var response = await PostFormAsync(
            client,
            EndpointPaths.DeviceAuthorization,
            new Dictionary<string, string> { ["client_id"] = ClientId, ["scope"] = "openid email" });
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("device_code").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("user_code").GetString()));
        Assert.Equal(5, body.RootElement.GetProperty("interval").GetInt32());
    }

    [Fact]
    public async Task ElDeviceCodeRespondeAuthorizationPendingMientrasElUsuarioNoAprueba()
    {
        using var client = Create();

        using var started = await PostFormAsync(
            client,
            EndpointPaths.DeviceAuthorization,
            new Dictionary<string, string> { ["client_id"] = ClientId, ["scope"] = "openid" });
        var deviceCode = (await ReadJsonAsync(started)).RootElement.GetProperty("device_code").GetString()!;

        using var polled = await PostFormAsync(
            client,
            EndpointPaths.Token,
            new Dictionary<string, string>
            {
                ["grant_type"] = GrantTypes.DeviceCode,
                ["client_id"] = ClientId,
                ["device_code"] = deviceCode
            });
        var body = await ReadJsonAsync(polled);

        Assert.Equal(HttpStatusCode.BadRequest, polled.StatusCode);
        Assert.Equal("authorization_pending", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CibaEmiteAuthReqIdYConLoginHintValidoElPrimerSondeoDaTokens()
    {
        using var client = Create();

        using var started = await PostFormAsync(
            client,
            EndpointPaths.Ciba,
            new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["login_hint"] = UserName,
                ["scope"] = "openid"
            });
        var authReqId = (await ReadJsonAsync(started)).RootElement.GetProperty("auth_req_id").GetString()!;

        using var polled = await PostFormAsync(
            client,
            EndpointPaths.Token,
            new Dictionary<string, string>
            {
                ["grant_type"] = GrantTypes.Ciba,
                ["client_id"] = ClientId,
                ["auth_req_id"] = authReqId
            });
        var body = await ReadJsonAsync(polled);

        Assert.Equal(HttpStatusCode.OK, polled.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("access_token").GetString()));
    }

    /// <summary>
    /// El discovery anuncia <c>backchannel_user_code_parameter_supported</c>, asi que un cliente que
    /// lo pida tiene que recibir un codigo por el que el usuario pueda identificar la peticion en
    /// una pantalla. Sin el, el mock afirma una capacidad que su propio endpoint no cumple.
    /// </summary>
    [Fact]
    public async Task CibaDevuelveUserCodeCuandoElClienteLoPide()
    {
        using var client = Create();

        using var started = await PostFormAsync(
            client,
            EndpointPaths.Ciba,
            new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["login_hint"] = UserName,
                ["scope"] = "openid",
                ["user_code_parameter_supported"] = "true"
            });

        var body = (await ReadJsonAsync(started)).RootElement;

        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("user_code").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("verification_uri").GetString()));
    }

    /// <summary>
    /// RFC 8628 3.1 y el contrato de los demás endpoints de cliente (token, PAR, introspect,
    /// revocation): un cliente confidencial tiene que autenticarse. Device authorization es la vía por
    /// la que un atacante pide un device code a nombre de otro cliente sin conocer su secreto.
    /// </summary>
    [Fact]
    public async Task DeviceAuthorizationRechazaSecretoIncorrectoConInvalidClient()
    {
        using var client = Create();

        using var response = await PostFormAsync(
            client,
            EndpointPaths.DeviceAuthorization,
            new Dictionary<string, string>
            {
                ["client_id"] = ServiceClientId,
                ["client_secret"] = "secreto-malo",
                ["scope"] = "openid"
            });
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_client", body.RootElement.GetProperty("error").GetString());
    }

    /// <summary>CIBA se inicia desde el backend del cliente, asi que la credencial es igual de necesaria.</summary>
    [Fact]
    public async Task CibaRechazaSecretoIncorrectoConInvalidClient()
    {
        using var client = Create();

        using var response = await PostFormAsync(
            client,
            EndpointPaths.Ciba,
            new Dictionary<string, string>
            {
                ["client_id"] = ServiceClientId,
                ["client_secret"] = "secreto-malo",
                ["login_hint"] = UserName,
                ["scope"] = "openid"
            });
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_client", body.RootElement.GetProperty("error").GetString());
    }

    /// <summary>Sin pedirlo, la respuesta se queda en lo minimo: el codigo es opcional.</summary>
    [Fact]
    public async Task CibaNoDevuelveUserCodeSiElClienteNoLoPide()
    {
        using var client = Create();

        using var started = await PostFormAsync(
            client,
            EndpointPaths.Ciba,
            new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["login_hint"] = UserName,
                ["scope"] = "openid"
            });

        var body = (await ReadJsonAsync(started)).RootElement;

        Assert.False(body.TryGetProperty("user_code", out _));
    }

    [Fact]
    public async Task CheckSessionSirveElIframeHtml()
    {
        using var client = Create();

        using var response = await client.GetAsync(
            $"{PathBase}/{EndpointPaths.CheckSession}",
            TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/html", response.Content.Headers.ContentType?.MediaType ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("postMessage", html, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> ValidParRequest() => new()
    {
        ["client_id"] = ClientId,
        ["redirect_uri"] = RedirectUri,
        ["response_type"] = "code",
        ["scope"] = "openid email",
        ["state"] = "st-1",
        ["code_challenge"] = Sha256Base64Url(CodeVerifier),
        ["code_challenge_method"] = "S256"
    };

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
}
