using System.Net;
using OidcMock.Core.Discovery;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// CORS solo en los endpoints que consume un navegador: discovery, JWKS, token y userinfo. El resto
/// (authorize con su formulario, introspect, revocation) no se expone a SPAs.
/// </summary>
public sealed class CorsTests
{
    private const string SpaOrigin = "http://localhost:5173";
    private const string ForeignOrigin = "http://localhost:9999";

    public static TheoryData<string, HttpMethod> EndpointsDeNavegador => new()
    {
        { EndpointPaths.Configuration, HttpMethod.Get },
        { EndpointPaths.Jwks, HttpMethod.Get },
        { EndpointPaths.Token, HttpMethod.Post },
        { EndpointPaths.UserInfo, HttpMethod.Get }
    };

    [Theory]
    [MemberData(nameof(EndpointsDeNavegador))]
    public async Task RespondeConAllowOriginAlOrigenAdmitido(string endpoint, HttpMethod method)
    {
        using var client = OidcTestClient.Create();

        using var response = await SendAsync(client, endpoint, method, SpaOrigin);

        Assert.Equal(SpaOrigin, AllowOrigin(response));
    }

    [Theory]
    [MemberData(nameof(EndpointsDeNavegador))]
    public async Task NoRespondeConAllowOriginAlOrigenNoAdmitido(string endpoint, HttpMethod method)
    {
        using var client = OidcTestClient.Create();

        using var response = await SendAsync(client, endpoint, method, ForeignOrigin);

        Assert.Null(AllowOrigin(response));
    }

    [Fact]
    public async Task ElPreflightDelTokenEndpointRespondeConLosMetodosYCabecerasQueLaSpaNecesita()
    {
        using var client = OidcTestClient.Create();
        using var preflight = new HttpRequestMessage(HttpMethod.Options, $"{OidcTestClient.PathBase}/{EndpointPaths.Token}");
        preflight.Headers.Add("Origin", SpaOrigin);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type,authorization");

        using var response = await client.SendAsync(preflight, TestContext.Current.CancellationToken);

        Assert.Equal(SpaOrigin, AllowOrigin(response));
        Assert.Contains("POST", response.Headers.GetValues("Access-Control-Allow-Methods"));
        Assert.Contains("authorization", response.Headers.GetValues("Access-Control-Allow-Headers"));
    }

    [Fact]
    public async Task ElUserinfoPermiteCredenciales()
    {
        using var client = OidcTestClient.Create();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{OidcTestClient.PathBase}/{EndpointPaths.UserInfo}");
        request.Headers.Add("Origin", SpaOrigin);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(SpaOrigin, AllowOrigin(response));
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    [Fact]
    public async Task ElFormularioDeLoginNoSeAbreAOrigenesDeSpa()
    {
        using var client = OidcTestClient.Create();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{OidcTestClient.PathBase}/{EndpointPaths.Authorize}?{QueryOfDefaultAuthorizeUrl()}");
        request.Headers.Add("Origin", SpaOrigin);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Null(AllowOrigin(response));
    }

    private static string QueryOfDefaultAuthorizeUrl() =>
        OidcTestClient.AuthorizeUrl().Split('?', StringSplitOptions.None)[1];

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string endpoint,
        HttpMethod method,
        string origin)
    {
        var request = new HttpRequestMessage(method, $"{OidcTestClient.PathBase}/{endpoint}");
        request.Headers.Add("Origin", origin);

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string? AllowOrigin(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values)
            ? values.Single()
            : null;
}