using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using OidcMock.Core.Discovery;
using OidcMock.Core.Grants;
using static OidcMock.IntegrationTests.Endpoints.OidcTestClient;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// Endpoints protegidos que consumen un token ya emitido: userinfo, introspect y revocation.
/// </summary>
public sealed class ProtectedEndpointTests
{
    [Fact]
    public async Task UserInfoDevuelveLosClaimsDelUsuario()
    {
        using var client = Create();
        var tokens = await RequestTokensAsync(client, GrantTypes.AuthorizationCode, await SignInAsync(client));

        using var response = await SendWithBearerAsync(client, EndpointPaths.UserInfo, tokens.AccessToken!);
        var claims = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("jperez@example.cr", claims.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task UserInfoRechazaUnaPeticionSinTokenCon401()
    {
        using var client = Create();

        using var response = await client.GetAsync(
            $"{PathBase}/{EndpointPaths.UserInfo}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserInfoRechazaUnTokenInvalidoCon401()
    {
        using var client = Create();

        using var response = await SendWithBearerAsync(client, EndpointPaths.UserInfo, "token-inventado");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task IntrospectInformaQueElAccessTokenEstaActivo()
    {
        using var client = Create();
        var tokens = await RequestTokensAsync(client, GrantTypes.AuthorizationCode, await SignInAsync(client));

        using var response = await PostFormAsync(
            client,
            EndpointPaths.Introspection,
            new Dictionary<string, string> { ["token"] = tokens.AccessToken!, ["client_id"] = ClientId });
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.RootElement.GetProperty("active").GetBoolean());
        Assert.Equal(ClientId, body.RootElement.GetProperty("client_id").GetString());
    }

    [Fact]
    public async Task IntrospectRespondeInactiveParaUnTokenDesconocido()
    {
        using var client = Create();

        using var response = await PostFormAsync(
            client,
            EndpointPaths.Introspection,
            new Dictionary<string, string> { ["token"] = "token-inventado", ["client_id"] = ClientId });
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.RootElement.GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task RevocationRespondeOkYElRefreshTokenDejaDeServir()
    {
        using var client = Create();
        var tokens = await RequestTokensAsync(client, GrantTypes.AuthorizationCode, await SignInAsync(client));

        using var revoked = await PostFormAsync(
            client,
            EndpointPaths.Revocation,
            new Dictionary<string, string> { ["token"] = tokens.RefreshToken!, ["client_id"] = ClientId });

        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);

        using var reuse = await client.PostAsync(
            $"{PathBase}/{EndpointPaths.Token}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = GrantTypes.RefreshToken,
                ["client_id"] = ClientId,
                ["refresh_token"] = tokens.RefreshToken!
            }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
    }

    [Fact]
    public async Task ClientCredentialsEmiteAccessTokenSinIdToken()
    {
        using var client = Create();

        var tokens = await RequestTokensAsync(
            client,
            GrantTypes.ClientCredentials,
            extra: new Dictionary<string, string>
            {
                ["client_id"] = ServiceClientId,
                ["client_secret"] = ServiceSecret
            });

        Assert.NotNull(tokens.AccessToken);
        Assert.Null(tokens.IdToken);
    }

    [Fact]
    public async Task ElTokenEndpointRespondeInvalidClientCon401YJson()
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
    public async Task ElTokenEndpointRespondeUnsupportedGrantTypeCon400()
    {
        using var client = Create();

        using var response = await PostFormAsync(
            client,
            EndpointPaths.Token,
            new Dictionary<string, string>
            {
                ["grant_type"] = "grant_inventado",
                ["client_id"] = ClientId
            });
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("unsupported_grant_type", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ElRefreshTokenCanjeableEmiteTokensNuevosYRotaElToken()
    {
        using var client = Create();
        var first = await RequestTokensAsync(client, GrantTypes.AuthorizationCode, await SignInAsync(client));

        var refreshed = await RequestTokensAsync(
            client,
            GrantTypes.RefreshToken,
            extra: new Dictionary<string, string> { ["refresh_token"] = first.RefreshToken! });

        Assert.NotNull(refreshed.AccessToken);
        Assert.NotEqual(first.RefreshToken, refreshed.RefreshToken);
    }

    private static Task<HttpResponseMessage> SendWithBearerAsync(HttpClient client, string path, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{PathBase}/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
}
