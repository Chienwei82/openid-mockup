using System.Net;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using OidcMock.Core.Grants;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// Pruebas del grant refresh_token por HTTP: rotacion obligatoria, deteccion de reutilizacion con
/// revocacion de la familia y scope que solo puede reducirse.
/// </summary>
public sealed class RefreshTokenGrantEndpointTests
{
    [Fact]
    public async Task CanjeaElRefreshTokenEmitidoPorElAuthorize()
    {
        using var client = OidcTestClient.Create();
        var code = await OidcTestClient.SignInAsync(client);
        var issued = await OidcTestClient.RequestTokensAsync(
            client,
            GrantTypes.AuthorizationCode,
            code,
            new Dictionary<string, string> { ["scope"] = "openid email offline_access" });

        var response = await OidcTestClient.PostTokenAsync(client, Form(issued.RefreshToken!));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CadaCanjeEmiteUnRefreshTokenDistinto()
    {
        using var client = OidcTestClient.Create();
        var code = await OidcTestClient.SignInAsync(client);
        var issued = await OidcTestClient.RequestTokensAsync(
            client,
            GrantTypes.AuthorizationCode,
            code,
            new Dictionary<string, string> { ["scope"] = "openid email offline_access" });

        var primero = await CanjeAsync(client, issued.RefreshToken!);
        var segundo = await CanjeAsync(client, primero);

        Assert.NotEqual(issued.RefreshToken, primero);
        Assert.NotEqual(primero, segundo);
    }

    [Fact]
    public async Task LaReutilizacionRevocaLaFamiliaYDevuelveInvalidGrant()
    {
        using var client = OidcTestClient.Create();
        var code = await OidcTestClient.SignInAsync(client);
        var issued = await OidcTestClient.RequestTokensAsync(
            client,
            GrantTypes.AuthorizationCode,
            code,
            new Dictionary<string, string> { ["scope"] = "openid email offline_access" });
        var rotado = await CanjeAsync(client, issued.RefreshToken!);

        var reutilizado = await OidcTestClient.PostTokenAsync(client, Form(issued.RefreshToken!));
        var posterior = await OidcTestClient.PostTokenAsync(client, Form(rotado));

        Assert.Equal("invalid_grant", await ErrorAsync(reutilizado));
        Assert.Equal("invalid_grant", await ErrorAsync(posterior));
    }

    [Fact]
    public async Task NoPermiteAmpliarElScopeDelRefreshTokenOriginal()
    {
        using var client = OidcTestClient.Create();
        var code = await OidcTestClient.SignInAsync(client);
        var issued = await OidcTestClient.RequestTokensAsync(
            client,
            GrantTypes.AuthorizationCode,
            code,
            new Dictionary<string, string> { ["scope"] = "openid email offline_access" });
        var form = Form(issued.RefreshToken!);
        form["scope"] = "openid email roles";

        var response = await OidcTestClient.PostTokenAsync(client, form);

        Assert.Equal("invalid_scope", await ErrorAsync(response));
    }

    [Fact]
    public async Task PermiteReducirElScopeDelRefreshTokenOriginal()
    {
        using var client = OidcTestClient.Create();
        var code = await OidcTestClient.SignInAsync(client);
        var issued = await OidcTestClient.RequestTokensAsync(
            client,
            GrantTypes.AuthorizationCode,
            code,
            new Dictionary<string, string> { ["scope"] = "openid email offline_access" });
        var form = Form(issued.RefreshToken!);
        form["scope"] = "openid";

        var response = await OidcTestClient.PostTokenAsync(client, form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// OpenID Connect Core 3.1.3.3: la renovacion de una sesion con scope openid devuelve un
    /// id_token nuevo, que es lo que espera cualquier cliente real al canjear su refresh token.
    /// </summary>
    [Fact]
    public async Task ElCanjeDevuelveUnIdTokenNuevoCuandoElScopeConservaOpenId()
    {
        using var client = OidcTestClient.Create();
        var code = await OidcTestClient.SignInAsync(client);
        var issued = await OidcTestClient.RequestTokensAsync(
            client,
            GrantTypes.AuthorizationCode,
            code,
            new Dictionary<string, string> { ["scope"] = "openid email offline_access" });

        var response = await OidcTestClient.PostTokenAsync(client, Form(issued.RefreshToken!));
        var body = JsonDocument.Parse(await ReadBodyAsync(response)).RootElement;

        var idToken = body.GetProperty("id_token").GetString();

        Assert.False(string.IsNullOrEmpty(idToken), "La renovacion tiene que devolver id_token.");
        Assert.Equal(OidcTestClient.UserSubject, ReadClaim(idToken!, "sub"));
    }

    private static async Task<string> CanjeAsync(HttpClient client, string refreshToken)
    {
        var response = await OidcTestClient.PostTokenAsync(client, Form(refreshToken));

        response.EnsureSuccessStatusCode();

        return ReadRefreshToken(await ReadBodyAsync(response));
    }

    private static Dictionary<string, string> Form(string refreshToken) => new()
    {
        ["grant_type"] = GrantTypes.RefreshToken,
        ["client_id"] = OidcTestClient.ClientId,
        ["refresh_token"] = refreshToken
    };

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    private static string ReadRefreshToken(string body) =>
        JsonDocument.Parse(body).RootElement.GetProperty("refresh_token").GetString()!;

    /// <summary>
    /// Lee un claim del payload de un JWT. No valida la firma: aqui solo se comprueba que el token
    /// emitido lleva el claim que tiene que llevar, y la validacion tiene sus propias pruebas.
    /// </summary>
    private static string ReadClaim(string jwt, string claim) =>
        new JsonWebToken(jwt).GetClaim(claim).Value;

    private static async Task<string> ErrorAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await ReadBodyAsync(response)).RootElement.GetProperty("error").GetString()!;
}