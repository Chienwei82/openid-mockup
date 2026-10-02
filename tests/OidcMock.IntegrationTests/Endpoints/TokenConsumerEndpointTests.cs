using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using OidcMock.Core.Discovery;
using OidcMock.Core.Grants;
using static OidcMock.IntegrationTests.Endpoints.OidcTestClient;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// Los tres endpoints que consumen un token ya emitido, por HTTP y con la matriz que pide el
/// enunciado: token valido, caducado, revocado, de otro cliente y sin autenticacion.
/// <para>
/// Los tokens imposibles de conseguir por el flujo (caducado, de otro cliente) se minan con la clave
/// real del host, para que la validacion de firma y de vigencia sea la de verdad.
/// </para>
/// </summary>
public sealed class TokenConsumerEndpointTests
{
    [Fact]
    public async Task UserInfoDevuelveLosClaimsDelUsuarioConUnTokenValido()
    {
        using var client = Create();
        var tokens = await RequestTokensAsync(client, GrantTypes.AuthorizationCode, await SignInAsync(client));

        using var response = await SendUserInfoAsync(client, tokens.AccessToken!);
        var claims = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("jperez@example.cr", claims.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task UserInfoAceptaTambienPost()
    {
        using var client = Create();
        var tokens = await RequestTokensAsync(client, GrantTypes.AuthorizationCode, await SignInAsync(client));

        using var response = await PostUserInfoAsync(client, tokens.AccessToken!);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// RFC 6750 3: el 401 de un recurso protegido debe traer el esquema de autenticacion en
    /// WWW-Authenticate, o el cliente no sabe que reintentar con un Bearer.
    /// </summary>
    [Fact]
    public async Task UserInfoSinTokenResponde401ConWwwAuthenticate()
    {
        using var client = Create();

        using var response = await client.GetAsync(
            $"{PathBase}/{EndpointPaths.UserInfo}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Bearer", Challenge(response), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UserInfoConTokenInvalidoResponde401ConInvalidTokenEnElReto()
    {
        using var client = Create();

        using var response = await SendUserInfoAsync(client, "token-inventado");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid_token", Challenge(response), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UserInfoConTokenCaducadoResponde401()
    {
        using var client = Create();
        using var minter = new ExpiredTokenMinter(ExpiredTokenMinter.IssuerFor(client, PathBase), ClientId);

        using var response = await SendUserInfoAsync(client, minter.Expired());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// El access token es un JWT sin estado: revocado tiene que significar "deja de servir", no "sigue
    /// valiendo hasta que expire". Antes de esta etapa, /revocation era un no-op para access tokens.
    /// </summary>
    [Fact]
    public async Task UserInfoConTokenRevocadoResponde401()
    {
        using var client = Create();
        var tokens = await RequestTokensAsync(client, GrantTypes.AuthorizationCode, await SignInAsync(client));
        await RevokeAsync(client, tokens.AccessToken!);

        using var response = await SendUserInfoAsync(client, tokens.AccessToken!);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// El access token es un JWT sin estado: revocado tiene que significar "deja de servir", no "sigue
    /// valiendo hasta que expire". Antes de esta etapa, /revocation era un no-op para access tokens.
    /// </summary>
    [Fact]
    public async Task IntrospectDeUnTokenValidoRespondeActivoConSusClaims()
    {
        using var client = Create();
        var tokens = await RequestTokensAsync(client, GrantTypes.AuthorizationCode, await SignInAsync(client));

        using var response = await IntrospectAsync(client, tokens.AccessToken!);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.RootElement.GetProperty("active").GetBoolean());
        Assert.Equal(ClientId, body.RootElement.GetProperty("client_id").GetString());
        Assert.Contains("openid", body.RootElement.GetProperty("scope").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IntrospectDeUnRefreshTokenRespondeActivo()
    {
        using var client = Create();
        var tokens = await RequestTokensAsync(client, GrantTypes.AuthorizationCode, await SignInAsync(client));

        using var response = await IntrospectAsync(client, tokens.RefreshToken!);
        var body = await ReadJsonAsync(response);

        Assert.True(body.RootElement.GetProperty("active").GetBoolean());
        Assert.Equal("refresh_token", body.RootElement.GetProperty("token_type").GetString());
    }

    /// <summary>
    /// RFC 7662 2.2: un token invalido, caducado o revocado se responde active=false con 200, nunca
    /// con un error: el endpoint no puede filtrar por que fallo.
    /// </summary>
    [Theory]
    [InlineData("token-inventado")]
    [InlineData("")]
    public async Task IntrospectDeUnTokenInvalidoRespondeInactiveCon200(string token)
    {
        using var client = Create();

        using var response = await IntrospectAsync(client, token);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.RootElement.GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task IntrospectDeUnTokenCaducadoRespondeInactive()
    {
        using var client = Create();
        using var minter = new ExpiredTokenMinter(ExpiredTokenMinter.IssuerFor(client, PathBase), ClientId);

        using var response = await IntrospectAsync(client, minter.Expired());
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.RootElement.GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task IntrospectDeUnTokenRevocadoRespondeInactive()
    {
        using var client = Create();
        var tokens = await RequestTokensAsync(client, GrantTypes.AuthorizationCode, await SignInAsync(client));
        await RevokeAsync(client, tokens.AccessToken!);

        using var response = await IntrospectAsync(client, tokens.AccessToken!);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.RootElement.GetProperty("active").GetBoolean());
    }

    /// <summary>
    /// Un cliente no puede preguntar por los tokens de otro: el token es valido, pero no es suyo, asi
    /// que la respuesta es tan poco reveladora como la de un token que no existe.
    /// </summary>
    [Fact]
    public async Task IntrospectDeUnTokenDeOtroClienteRespondeInactive()
    {
        using var client = Create();
        var tokens = await ServiceTokensAsync(client);

        using var response = await IntrospectAsync(client, tokens.AccessToken!);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.RootElement.GetProperty("active").GetBoolean());
    }

    /// <summary>
    /// RFC 7662 2.1: introspect exige autenticacion del cliente. Sin ella el endpoint seria una
    /// forma de consultar el estado de cualquier token del mock.
    /// </summary>
    [Fact]
    public async Task IntrospectSinAutenticarElClienteResponde401()
    {
        using var client = Create();
        var tokens = await RequestTokensAsync(client, GrantTypes.AuthorizationCode, await SignInAsync(client));

        using var response = await client.PostAsync(
            $"{PathBase}/{EndpointPaths.Introspection}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = tokens.AccessToken! }),
            TestContext.Current.CancellationToken);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_client", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task IntrospectAceptaAutenticacionBasicaDelCliente()
    {
        using var client = Create();
        var tokens = await ServiceTokensAsync(client);

        using var response = await OidcTestClient.PostWithBasicAuthAsync(
            client,
            EndpointPaths.Introspection,
            new Dictionary<string, string> { ["token"] = tokens.AccessToken! },
            ServiceClientId,
            ServiceSecret);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.RootElement.GetProperty("active").GetBoolean());
    }

    /// <summary>
    /// RFC 7009 2.2: revocar un token desconocido responde 200 igual que revocar uno conocido, para no
    /// revelar si existio.
    /// </summary>
    [Fact]
    public async Task RevocarUnTokenDesconocidoResponde200()
    {
        using var client = Create();

        using var response = await RevokeAsync(client, "token-que-nunca-existio");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RevocarUnAccessTokenLoDejaDeServirEnUserInfo()
    {
        using var client = Create();
        var tokens = await RequestTokensAsync(client, GrantTypes.AuthorizationCode, await SignInAsync(client));

        using var revoked = await RevokeAsync(client, tokens.AccessToken!);
        using var userInfo = await SendUserInfoAsync(client, tokens.AccessToken!);

        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, userInfo.StatusCode);
    }

    [Fact]
    public async Task RevocarUnRefreshTokenImpideRenovarLaSesion()
    {
        using var client = Create();
        var tokens = await RequestTokensAsync(client, GrantTypes.AuthorizationCode, await SignInAsync(client));

        using var revoked = await RevokeAsync(client, tokens.RefreshToken!);
        using var renewal = await PostTokenAsync(client, new Dictionary<string, string>
        {
            ["grant_type"] = GrantTypes.RefreshToken,
            ["client_id"] = ClientId,
            ["refresh_token"] = tokens.RefreshToken!
        });

        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, renewal.StatusCode);
    }

    /// <summary>
    /// RFC 7009: revocar un refresh token invalida la familia, asi que el token siguiente de la
    /// rotacion tampoco sirve. Sin la cascada, cerrar sesion dejaba vivo al ya rotado.
    /// </summary>
    [Fact]
    public async Task RevocarUnRefreshTokenInvalidaElSiguienteDeSuFamilia()
    {
        using var client = Create();
        var tokens = await RequestTokensAsync(client, GrantTypes.AuthorizationCode, await SignInAsync(client));
        var rotado = await RotateAsync(client, tokens.RefreshToken!);

        using var revoked = await RevokeAsync(client, tokens.RefreshToken!);
        using var renewal = await PostTokenAsync(client, new Dictionary<string, string>
        {
            ["grant_type"] = GrantTypes.RefreshToken,
            ["client_id"] = ClientId,
            ["refresh_token"] = rotado
        });

        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, renewal.StatusCode);
    }

    [Fact]
    public async Task RevocarElTokenDeOtroClienteResponde200PeroNoLoRevoca()
    {
        using var client = Create();
        var tokens = await ServiceTokensAsync(client);

        using var response = await RevokeAsync(client, tokens.AccessToken!);
        using var introspected = await IntrospectForServiceAsync(client, tokens.AccessToken!);
        var body = await ReadJsonAsync(introspected);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.RootElement.GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task RevocationSinAutenticarElClienteResponde401()
    {
        using var client = Create();
        var tokens = await RequestTokensAsync(client, GrantTypes.AuthorizationCode, await SignInAsync(client));

        using var response = await client.PostAsync(
            $"{PathBase}/{EndpointPaths.Revocation}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = tokens.AccessToken! }),
            TestContext.Current.CancellationToken);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_client", body.RootElement.GetProperty("error").GetString());
    }

    /// <summary>
    /// Con el secreto equivocado tampoco se revoca: sin esto, un atacante que supiera el client_id de
    /// una victima podria cerrarle las sesiones.
    /// </summary>
    [Fact]
    public async Task RevocarConSecretoIncorrectoResponde401YNoRevoca()
    {
        using var client = Create();
        var tokens = await ServiceTokensAsync(client);

        using var response = await client.PostAsync(
            $"{PathBase}/{EndpointPaths.Revocation}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = tokens.AccessToken!,
                ["client_id"] = ServiceClientId,
                ["client_secret"] = "secreto-incorrecto"
            }),
            TestContext.Current.CancellationToken);
        using var introspected = await IntrospectForServiceAsync(client, tokens.AccessToken!);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True((await ReadJsonAsync(introspected)).RootElement.GetProperty("active").GetBoolean());
    }

    /// <summary>Tokens de un cliente confidencial distinto, para probar la pertenencia de un token.</summary>
    private static Task<IssuedTokens> ServiceTokensAsync(HttpClient client) =>
        RequestTokensAsync(client, GrantTypes.ClientCredentials, extra: new Dictionary<string, string>
        {
            ["client_id"] = ServiceClientId,
            ["client_secret"] = ServiceSecret
        });

    private static Task<HttpResponseMessage> SendUserInfoAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{PathBase}/{EndpointPaths.UserInfo}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> PostUserInfoAsync(HttpClient client, string accessToken) =>
        client.PostAsync(
            $"{PathBase}/{EndpointPaths.UserInfo}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["access_token"] = accessToken }),
            TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> IntrospectAsync(HttpClient client, string token) =>
        OidcTestClient.PostFormAsync(client, EndpointPaths.Introspection, new Dictionary<string, string>
        {
            ["token"] = token,
            ["client_id"] = ClientId
        });

    private static Task<HttpResponseMessage> IntrospectForServiceAsync(HttpClient client, string token) =>
        OidcTestClient.PostWithBasicAuthAsync(
            client,
            EndpointPaths.Introspection,
            new Dictionary<string, string> { ["token"] = token },
            ServiceClientId,
            ServiceSecret);

    private static Task<HttpResponseMessage> RevokeAsync(HttpClient client, string token) =>
        OidcTestClient.PostFormAsync(client, EndpointPaths.Revocation, new Dictionary<string, string>
        {
            ["token"] = token,
            ["client_id"] = ClientId
        });

    private static async Task<string> RotateAsync(HttpClient client, string refreshToken)
    {
        using var response = await PostTokenAsync(client, new Dictionary<string, string>
        {
            ["grant_type"] = GrantTypes.RefreshToken,
            ["client_id"] = ClientId,
            ["refresh_token"] = refreshToken
        });

        response.EnsureSuccessStatusCode();

        return (await ReadJsonAsync(response)).RootElement.GetProperty("refresh_token").GetString()!;
    }

    private static string Challenge(HttpResponseMessage response) =>
        response.Headers.WwwAuthenticate.ToString();

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
}