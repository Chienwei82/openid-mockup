using System.Net;
using System.Text.Json;
using OidcMock.Core.Grants;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// Pruebas del grant client_credentials por HTTP: solo clientes confidenciales, con sub igual al
/// client_id, sin id_token ni refresh token y con los scopes validados contra allowed_scopes.
/// </summary>
public sealed class ClientCredentialsGrantEndpointTests
{
    [Fact]
    public async Task EmiteAccessTokenParaElClienteConfidencial()
    {
        using var client = OidcTestClient.Create();

        var response = await OidcTestClient.PostTokenAsync(client, Form());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ElSubjectDelAccessTokenEsElClientId()
    {
        using var client = OidcTestClient.Create();

        var response = await OidcTestClient.PostTokenAsync(client, Form());
        var body = JsonDocument.Parse(await ReadBodyAsync(response)).RootElement;
        var claims = OidcTestClient.ReadJwtPayload(body.GetProperty("access_token").GetString()!);

        Assert.Equal(OidcTestClient.ServiceClientId, claims.GetProperty("sub").GetString());
    }

    [Fact]
    public async Task NoEmiteIdTokenNiRefreshToken()
    {
        using var client = OidcTestClient.Create();

        var body = JsonDocument.Parse(
            await ReadBodyAsync(await OidcTestClient.PostTokenAsync(client, Form()))).RootElement;

        Assert.False(body.TryGetProperty("id_token", out _));
        Assert.False(body.TryGetProperty("refresh_token", out _));
    }

    [Fact]
    public async Task AnunciaBearerYElScopeConcedido()
    {
        using var client = OidcTestClient.Create();

        var body = JsonDocument.Parse(
            await ReadBodyAsync(await OidcTestClient.PostTokenAsync(client, Form()))).RootElement;

        Assert.Equal("Bearer", body.GetProperty("token_type").GetString());
        Assert.Equal("custom.profile", body.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task RechazaUnScopeNoPermitidoParaElCliente()
    {
        using var client = OidcTestClient.Create();
        var form = Form();
        form["scope"] = "offline_access";

        var response = await OidcTestClient.PostTokenAsync(client, form);

        Assert.Equal("invalid_scope", await ErrorAsync(response));
    }

    [Fact]
    public async Task RechazaUnClienteQueNoTieneElGrantPermitido()
    {
        using var client = OidcTestClient.Create();
        var form = Form();
        form["client_id"] = OidcTestClient.ClientId;

        var response = await OidcTestClient.PostTokenAsync(client, form);

        Assert.Equal("unsupported_grant_type", await ErrorAsync(response));
    }

    [Fact]
    public async Task RechazaUnSecretoIncorrecto()
    {
        using var client = OidcTestClient.Create();
        var form = Form();
        form["client_secret"] = "no-es-el-secreto";

        var response = await OidcTestClient.PostTokenAsync(client, form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Dictionary<string, string> Form() => new()
    {
        ["grant_type"] = GrantTypes.ClientCredentials,
        ["client_id"] = OidcTestClient.ServiceClientId,
        ["client_secret"] = OidcTestClient.ServiceSecret,
        ["scope"] = "custom.profile"
    };

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    private static async Task<string> ErrorAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await ReadBodyAsync(response)).RootElement.GetProperty("error").GetString()!;
}