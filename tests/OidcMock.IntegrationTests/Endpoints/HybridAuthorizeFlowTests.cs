using System.Collections.Specialized;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using OidcMock.Core.Crypto;
using OidcMock.Core.Discovery;
using OidcMock.Core.Grants;
using static OidcMock.IntegrationTests.Endpoints.OidcTestClient;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// Comportamiento observable del flujo hibrido (response_type=code id_token) tal como lo emite el
/// servidor real de la empresa: code, id_token y session_state llegan juntos en el fragmento del
/// redirect_uri, el id_token lleva c_hash del code y eco del nonce, y los valores tienen el formato
/// capturado (code en hexadecimal con sufijo, session_state en dos partes separadas por punto).
/// </summary>
public sealed class HybridAuthorizeFlowTests
{
    private const string HybridResponseType = "code id_token";
    private const string Nonce = "n-1";
    private const string State = "st-1";

    [Fact]
    public async Task LaRespuestaHibridaDevuelveCodeIdTokenYSessionStateEnElFragmento()
    {
        using var client = Create();

        var (location, _) = await SignInToHybridAsync(client);
        var fragment = ReadFragment(location);

        Assert.Contains("#", location.OriginalString, StringComparison.Ordinal);
        Assert.DoesNotContain("?code=", location.OriginalString, StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(fragment["code"]));
        Assert.False(string.IsNullOrEmpty(fragment["id_token"]));
        Assert.False(string.IsNullOrEmpty(fragment["session_state"]));
        Assert.Equal(State, fragment["state"]);
    }

    [Fact]
    public async Task ElIdTokenHibridoLlevaCHashDelCodeYElNonceEcoado()
    {
        using var client = Create();

        var fragment = ReadFragment((await SignInToHybridAsync(client)).Location);
        var payload = ReadJwtPayload(fragment["id_token"]!);

        Assert.Equal(Nonce, payload.GetProperty("nonce").GetString());
        Assert.Equal(ClientId, payload.GetProperty("aud").GetString());
        Assert.Equal(UserSubject, payload.GetProperty("sub").GetString());
        Assert.Equal(HashLeftHalf(fragment["code"]!), payload.GetProperty("c_hash").GetString());
    }

    /// <summary>
    /// La forma exacta de la captura del servidor real: el fragmento empieza por #code= (sin un ?
    /// de query string de mas) y la respuesta hibrida no lleva el parametro iss, que el servidor
    /// real omite aqui pese a anunciarlo en el discovery.
    /// </summary>
    [Fact]
    public async Task LaRespuestaHibridaTieneLaFormaExactaDeLaCapturaDelServidorReal()
    {
        using var client = Create();

        var location = (await SignInToHybridAsync(client)).Location;

        Assert.Contains("#code=", location.OriginalString, StringComparison.Ordinal);
        Assert.DoesNotContain("#?", location.OriginalString, StringComparison.Ordinal);
        Assert.DoesNotContain("iss=", location.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ElCodeYElSessionStateTienenElFormatoObservadoEnElServidorReal()
    {
        using var client = Create();

        var fragment = ReadFragment((await SignInToHybridAsync(client)).Location);

        Assert.Matches("^[0-9A-F]{64}-1$", fragment["code"]);
        Assert.Matches(@"^[A-Za-z0-9_-]{43}\.[0-9A-F]{32}$", fragment["session_state"]);
    }

    /// <summary>
    /// El sid del id_token es la sesion del navegador que abrio el login, en el formato que emite
    /// el servidor real (32 hexadecimales en mayuscula), y viaja igual en todos los id_token.
    /// </summary>
    [Fact]
    public async Task ElIdTokenDeclaraLaSesionDelNavegadorEnSid()
    {
        using var client = Create();

        var (location, sessionId) = await SignInToHybridAsync(client);
        var payload = ReadJwtPayload(ReadFragment(location)["id_token"]!);

        Assert.Matches("^[0-9A-F]{32}$", sessionId);
        Assert.Equal(sessionId, payload.GetProperty("sid").GetString());
    }

    [Fact]
    public async Task ElIdTokenDelCanjeDelCodeDeclaraLaMismaSesionQueElHibrido()
    {
        using var client = Create();

        var (location, _) = await SignInToHybridAsync(client);
        var fragment = ReadFragment(location);
        var tokens = await RequestTokensAsync(client, GrantTypes.AuthorizationCode, fragment["code"]);

        var hybridSid = ReadJwtPayload(fragment["id_token"]!).GetProperty("sid").GetString();

        Assert.Equal(hybridSid, ReadJwtPayload(tokens.IdToken!).GetProperty("sid").GetString());
    }

    /// <summary>
    /// Recorre el login completo con response_type=code id_token y devuelve el Location de la
    /// redireccion final al redirect_uri y el identificador de la sesion que abrio el login, que es
    /// lo que el navegador llevaria en su cookie desde ese momento.
    /// </summary>
    private static async Task<(Uri Location, string SessionId)> SignInToHybridAsync(HttpClient client)
    {
        var parameters = DefaultParameters();
        parameters["response_type"] = HybridResponseType;

        using var loginPage = await client.GetAsync(
            BuildAuthorizeUrl(parameters),
            TestContext.Current.CancellationToken);
        var fields = LoginFormFields.Parse(
            await loginPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        fields["action"] = "accept";

        using var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, $"{PathBase}/{EndpointPaths.Authorize}")
            {
                Content = new FormUrlEncodedContent(fields)
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return (response.Headers.Location!, SessionIdFrom(response));
    }

    private static string SessionIdFrom(HttpResponseMessage response)
    {
        var name = OidcMock.Host.Endpoints.AuthSessionCookie.Name;
        var setCookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith($"{name}=", StringComparison.Ordinal));
        var pair = setCookie.Split(';', 2)[0];

        return pair[(name.Length + 1)..];
    }

    /// <summary>
    /// Los parametros de la respuesta hibrida viajan en el fragmento, que Uri.Query no ve: se
    /// trabajan sobre el texto del Location, igual que ReadCode con el query string.
    /// </summary>
    private static NameValueCollection ReadFragment(Uri location)
    {
        var hashIndex = location.OriginalString.IndexOf('#', StringComparison.Ordinal);

        Assert.True(hashIndex >= 0, $"La redireccion '{location}' no llevaba fragmento.");
        return HttpUtility.ParseQueryString(location.OriginalString[(hashIndex + 1)..]);
    }

    /// <summary>c_hash es la mitad izquierda del SHA-256 del code, en base64url (OIDC Core 3.3.2.11).</summary>
    private static string HashLeftHalf(string value)
    {
        var digest = SHA256.HashData(Encoding.ASCII.GetBytes(value));

        return Base64Url.Encode(digest.AsSpan(0, digest.Length / 2));
    }
}