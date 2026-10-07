using System.Collections.Specialized;
using System.Net;
using System.Web;
using OidcMock.Core.Discovery;
using OidcMock.Core.Grants;
using static OidcMock.IntegrationTests.Endpoints.OidcTestClient;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// Rutas de entrada del servidor real, tal como arranca la prueba contra GAUDI:
/// /Account/Login?ReturnUrl=... es la pantalla de login y /connect/authorize/callback el destino
/// del ReturnUrl. El flujo completo termina igual que la captura: el redirect_uri con #code,
/// #id_token y #session_state en el fragmento. Las rutas se fijan por su texto porque son el
/// contrato observable con la prueba.
/// </summary>
public sealed class AccountLoginEndpointTests
{
    private const string LoginPath = "Account/Login";
    private const string AuthorizeCallbackPath = "connect/authorize/callback";
    private const string ReturnUrlField = "ReturnUrl";
    private const string Accept = "accept";
    private const string Deny = "deny";

    private static readonly string CallbackUrl =
        BuildAuthorizeUrl(HybridParameters()).Replace(
            EndpointPaths.Authorize, AuthorizeCallbackPath, StringComparison.Ordinal);

    [Fact]
    public async Task AccountLoginMuestraLaPantallaDeIdentidadConElReturnUrlOculto()
    {
        using var client = Create();

        using var response = await client.GetAsync(LoginEntryUrl(CallbackUrl), TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var fields = LoginFormFields.Parse(html);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(CallbackUrl, WebUtility.HtmlDecode(fields[ReturnUrlField]));
        Assert.Contains("name=\"sub\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"password\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AceptarEnAccountLoginRedirigeAlReturnUrl()
    {
        using var client = Create();

        using var response = await PostLoginAsync(client, CallbackUrl, Accept);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(CallbackUrl, response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task DenegarEnAccountLoginVuelveAMostrarElFormulario()
    {
        using var client = Create();

        using var response = await PostLoginAsync(client, CallbackUrl, Deny);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(ReturnUrlField, html, StringComparison.Ordinal);
        Assert.Contains("name=\"sub\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnReturnUrlExternoSeRechazaSinRedirigir()
    {
        using var client = Create();
        const string External = "https://atacante.example/robo";

        using var shown = await client.GetAsync(LoginEntryUrl(External), TestContext.Current.CancellationToken);
        using var posted = await client.PostAsync(
            $"{PathBase}/{LoginPath}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["action"] = Accept,
                [ReturnUrlField] = External
            }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, shown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, posted.StatusCode);
        Assert.Null(posted.Headers.Location);
    }

    /// <summary>
    /// El flujo completo con la forma de GAUDI: entrada por Account/Login, login por POST, el
    /// ReturnUrl cae en el callback del authorize y este devuelve el fragmento hibrido al
    /// redirect_uri. Es la prueba del script, contra el mock.
    /// </summary>
    [Fact]
    public async Task ElFlujoDeEntradaDelServidorRealTerminaEnElFragmentoDelHibrido()
    {
        using var client = Create();

        using var signIn = await PostLoginAsync(client, CallbackUrl, Accept);
        Assert.Equal(CallbackUrl, signIn.Headers.Location!.OriginalString);

        using var granted = await client.GetAsync(signIn.Headers.Location!, TestContext.Current.CancellationToken);
        var fragment = ReadFragment(granted.Headers.Location!);

        Assert.Equal(HttpStatusCode.Redirect, granted.StatusCode);
        Assert.False(string.IsNullOrEmpty(fragment["code"]));
        Assert.False(string.IsNullOrEmpty(fragment["id_token"]));
        Assert.False(string.IsNullOrEmpty(fragment["session_state"]));
    }

    private static Dictionary<string, string?> HybridParameters()
    {
        var parameters = DefaultParameters();
        parameters["response_type"] = "code id_token";
        return parameters;
    }

    private static string LoginEntryUrl(string returnUrl) =>
        $"{PathBase}/{LoginPath}?{ReturnUrlField}={Uri.EscapeDataString(returnUrl)}";

    /// <summary>
    /// Abre la pantalla de identidad y la envia como lo haria el navegador, con la decision elegida.
    /// El valor del ReturnUrl viene entizado en el HTML (el separador de parametros es &amp;) y el
    /// navegador lo entrega decodificado, asi que la prueba hace lo mismo antes de responder al
    /// formulario.
    /// </summary>
    private static async Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string returnUrl, string action)
    {
        using var loginPage = await client.GetAsync(LoginEntryUrl(returnUrl), TestContext.Current.CancellationToken);
        var fields = LoginFormFields.Parse(await loginPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        fields["action"] = action;
        fields[ReturnUrlField] = WebUtility.HtmlDecode(fields[ReturnUrlField]);

        return await client.PostAsync(
            $"{PathBase}/{LoginPath}",
            new FormUrlEncodedContent(fields),
            TestContext.Current.CancellationToken);
    }

    private static NameValueCollection ReadFragment(Uri location) =>
        HttpUtility.ParseQueryString(location.OriginalString.Split('#', 2)[1]);
}