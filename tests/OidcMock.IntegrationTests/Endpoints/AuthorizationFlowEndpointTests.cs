using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OidcMock.Core.Discovery;
using OidcMock.Core.Grants;
using OidcMock.Host;
using static OidcMock.IntegrationTests.Endpoints.OidcTestClient;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// Recorre el flujo de authorization code completo contra el host real: authorize, pantalla de login
/// y canje del codigo en el token endpoint.
/// </summary>
public sealed class AuthorizationFlowEndpointTests
{
    [Fact]
    public async Task AuthorizeMuestraLaPantallaDeLoginConElBrandingDelCliente()
    {
        using var client = Create();

        using var response = await client.GetAsync(AuthorizeUrl(), TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/html", response.Content.Headers.ContentType?.MediaType ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("web-app-spa", html, StringComparison.Ordinal);
        Assert.Contains("name=\"password\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizeRechazaUnRedirectUriNoRegistradoSinRedirigir()
    {
        using var client = Create();

        using var response = await client.GetAsync(
            $"{PathBase}/{EndpointPaths.Authorize}" +
            $"?client_id={ClientId}&redirect_uri=https%3A%2F%2Fatacante.example%2Fcb&response_type=code&scope=openid",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuthorizeConCredencialesIncorrectasRedirigeConErrorYNoEmiteCode()
    {
        using var client = Create();
        using var loginPage = await client.GetAsync(AuthorizeUrl(), TestContext.Current.CancellationToken);
        var fields = LoginFormFields.Parse(await loginPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        fields["username"] = UserName;
        fields["password"] = "clave-incorrecta";

        using var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, $"{PathBase}/{EndpointPaths.Authorize}")
            {
                Content = new FormUrlEncodedContent(fields)
            },
            TestContext.Current.CancellationToken);

        // El redirect_uri ya es valido, asi que el error viaja por el, tal como exige OAuth.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("error=access_denied", location, StringComparison.Ordinal);
        Assert.DoesNotContain("code=", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ElFlujoCompletoEntregaTokensDesdeElCode()
    {
        using var client = Create();

        var tokens = await RequestTokensAsync(client, GrantTypes.AuthorizationCode, await SignInAsync(client));

        Assert.NotNull(tokens.AccessToken);
        Assert.NotNull(tokens.IdToken);
        Assert.NotNull(tokens.RefreshToken);
    }

    [Fact]
    public async Task ElTokenEndpointAnunciaBearerYExpiresIn()
    {
        using var client = Create();

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = GrantTypes.AuthorizationCode,
            ["client_id"] = ClientId,
            ["redirect_uri"] = RedirectUri,
            ["code_verifier"] = CodeVerifier,
            ["code"] = await SignInAsync(client)
        });
        using var response = await client.PostAsync($"{PathBase}/{EndpointPaths.Token}", form, TestContext.Current.CancellationToken);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Bearer", body.RootElement.GetProperty("token_type").GetString());
        Assert.True(body.RootElement.GetProperty("expires_in").GetInt64() > 0);
    }

    [Fact]
    public async Task ElCodeSirveDeUnSoloUso()
    {
        using var client = Create();
        var code = await SignInAsync(client);

        var first = await RequestTokensAsync(client, GrantTypes.AuthorizationCode, code);

        using var second = await client.PostAsync(
            $"{PathBase}/{EndpointPaths.Token}",
            TokenForm(code),
            TestContext.Current.CancellationToken);

        Assert.NotNull(first.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var body = JsonDocument.Parse(await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("invalid_grant", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ElCodeRechazaUnCodeVerifierIncorrecto()
    {
        using var client = Create();
        var code = await SignInAsync(client);

        using var response = await client.PostAsync(
            $"{PathBase}/{EndpointPaths.Token}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = GrantTypes.AuthorizationCode,
                ["client_id"] = ClientId,
                ["redirect_uri"] = RedirectUri,
                ["code"] = code,
                ["code_verifier"] = "otro-verificador-que-no-corresponde-000000000000000"
            }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static FormUrlEncodedContent TokenForm(string code) =>
        new(new Dictionary<string, string>
        {
            ["grant_type"] = GrantTypes.AuthorizationCode,
            ["client_id"] = ClientId,
            ["redirect_uri"] = RedirectUri,
            ["code_verifier"] = CodeVerifier,
            ["code"] = code
        });
}