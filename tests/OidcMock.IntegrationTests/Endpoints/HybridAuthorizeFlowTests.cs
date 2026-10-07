using System.Collections.Specialized;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using OidcMock.Core.Crypto;
using OidcMock.Core.Discovery;
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

        var location = await SignInToHybridAsync(client);
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

        var fragment = ReadFragment(await SignInToHybridAsync(client));
        var payload = ReadJwtPayload(fragment["id_token"]!);

        Assert.Equal(Nonce, payload.GetProperty("nonce").GetString());
        Assert.Equal(ClientId, payload.GetProperty("aud").GetString());
        Assert.Equal(UserSubject, payload.GetProperty("sub").GetString());
        Assert.Equal(HashLeftHalf(fragment["code"]!), payload.GetProperty("c_hash").GetString());
    }

    [Fact]
    public async Task ElCodeYElSessionStateTienenElFormatoObservadoEnElServidorReal()
    {
        using var client = Create();

        var fragment = ReadFragment(await SignInToHybridAsync(client));

        Assert.Matches("^[0-9A-F]{64}-1$", fragment["code"]);
        Assert.Matches(@"^[A-Za-z0-9_-]{43}\.[0-9A-F]{32}$", fragment["session_state"]);
    }

    /// <summary>
    /// Recorre el login completo con response_type=code id_token y devuelve el Location de la
    /// redireccion final al redirect_uri: es justo lo que veria el navegador en la barra.
    /// </summary>
    private static async Task<Uri> SignInToHybridAsync(HttpClient client)
    {
        var parameters = DefaultParameters();
        parameters["response_type"] = HybridResponseType;

        using var loginPage = await client.GetAsync(
            BuildAuthorizeUrl(parameters),
            TestContext.Current.CancellationToken);
        var fields = LoginFormFields.Parse(
            await loginPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        fields["username"] = UserName;
        fields["password"] = Password;

        using var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, $"{PathBase}/{EndpointPaths.Authorize}")
            {
                Content = new FormUrlEncodedContent(fields)
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return response.Headers.Location!;
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