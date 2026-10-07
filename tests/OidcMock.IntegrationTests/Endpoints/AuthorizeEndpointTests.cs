using System.Net;
using System.Text.Encodings.Web;
using System.Web;
using OidcMock.Core.Authorization;
using OidcMock.Core.Discovery;
using OidcMock.Core.Grants;
using static OidcMock.IntegrationTests.Endpoints.OidcTestClient;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// Valores posibles de la decision de consentimiento. Se declaran aqui para que el test y el HTML
/// no dependan de cadenas sueltas.
/// </summary>
public static class ConsentDecision
{
    public const string Allow = "allow";
    public const string Deny = "deny";
}

/// <summary>Campo del formulario de consentimiento que lleva la decision del usuario.</summary>
public static class ConsentFormFields
{
    public const string Decision = "decision";
}

/// <summary>
/// Comportamiento observable de GET/POST /connect/authorize: que regla de la cadena falla y si el
/// error se ve en pantalla o viaja por el redirect_uri, la respuesta con code+state+iss, y las
/// pantallas de login, consentimiento y respuesta por form_post.
/// </summary>
public sealed class AuthorizeEndpointTests
{
    [Fact]
    public async Task UnClientIdDesconocidoSeMuestraEnPantallaYNoRedirige()
    {
        using var client = Create();

        using var response = await client.GetAsync(
            AuthorizeUrl(clientId: "cliente-inexistente"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Contains("invalid_client", await ReadBodyAsync(response), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnRedirectUriNoRegistradoSeMuestraEnPantallaYNoRedirige()
    {
        using var client = Create();

        using var response = await client.GetAsync(
            AuthorizeUrl(redirectUri: "https://atacante.example/cb"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task UnResponseTypeNoSoportadoRedirigeConErrorYState()
    {
        using var client = Create();

        using var response = await client.GetAsync(
            AuthorizeUrl(responseType: "code inventado"),
            TestContext.Current.CancellationToken);

        AssertRedirectsWith(response, "unsupported_response_type");
    }

    /// <summary>
    /// El discovery anuncia combinaciones que el mock no emite. Aceptarlas seria peor que
    /// rechazarlas: el cliente pediria tokens en el fragmento y recibiria un code que no sabe
    /// usar, sin ningun error que lo indique.
    /// </summary>
    [Theory]
    [InlineData("id_token")]
    [InlineData("token")]
    [InlineData("id_token token")]
    [InlineData("code token")]
    [InlineData("code id_token token")]
    public async Task UnResponseTypeAnunciadoPeroNoEmitidoRedirigeConUnsupportedResponseType(string responseType)
    {
        using var client = Create();

        using var response = await client.GetAsync(
            AuthorizeUrl(responseType: responseType),
            TestContext.Current.CancellationToken);

        AssertRedirectsWith(response, "unsupported_response_type");
        Assert.DoesNotContain("code=", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnScopeNoPermitidoParaElClienteRedirigeConInvalidScope()
    {
        using var client = Create();

        using var response = await client.GetAsync(
            AuthorizeUrl(scope: "openid scope-prohibido"),
            TestContext.Current.CancellationToken);

        AssertRedirectsWith(response, "invalid_scope");
    }

    [Fact]
    public async Task UnClienteQueExigePkceRedirigeConInvalidRequestSiNoTraeCodeChallenge()
    {
        using var client = Create();

        using var response = await client.GetAsync(
            AuthorizeUrl(withPkce: false),
            TestContext.Current.CancellationToken);

        AssertRedirectsWith(response, "invalid_request");
    }

    [Fact]
    public async Task UnCodeChallengeMethodNoSoportadoRedirigeConInvalidRequest()
    {
        using var client = Create();

        using var response = await client.GetAsync(
            AuthorizeUrl(codeChallengeMethod: "MD5"),
            TestContext.Current.CancellationToken);

        AssertRedirectsWith(response, "invalid_request");
    }

    [Fact]
    public async Task UnResponseModeNoSoportadoRedirigeConInvalidRequest()
    {
        using var client = Create();

        using var response = await client.GetAsync(
            AuthorizeUrl(responseMode: "form_get"),
            TestContext.Current.CancellationToken);

        AssertRedirectsWith(response, "invalid_request");
    }

    [Fact]
    public async Task ElLoginRedirigeConCodeStateEIss()
    {
        using var client = Create();

        using var response = await SignInAsync(client, AuthorizeUrl());

        var location = AssertRedirectedToClient(response);
        Assert.Contains("code=", location, StringComparison.Ordinal);
        Assert.Contains("state=st-1", location, StringComparison.Ordinal);
        Assert.Contains("iss=", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ElCodeEmitidoEsDeUnSoloUso()
    {
        using var client = Create();
        var code = await SignInForCodeAsync(client);

        using var first = await PostTokenAsync(client, code);
        using var second = await PostTokenAsync(client, code);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task ResponseModeFormPostDevuelveUnFormularioAutoEnviable()
    {
        using var client = Create();

        using var response = await SignInAsync(
            client,
            AuthorizeUrl(responseMode: ResponseModes.FormPost));

        var html = await ReadBodyAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"action=\"{RedirectUri}\"", html, StringComparison.Ordinal);
        Assert.Contains("document.forms[0].submit()", html, StringComparison.Ordinal);
        Assert.Contains("name=\"code\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"state\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"iss\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PromptNoneSinSesionRedirigeConLoginRequired()
    {
        using var client = Create();

        using var response = await client.GetAsync(
            AuthorizeUrl(prompt: PromptValues.None),
            TestContext.Current.CancellationToken);

        AssertRedirectsWith(response, "login_required");
    }

    [Fact]
    public async Task PromptNoneConSesionRedirigeConCodeSinPantalla()
    {
        using var client = Create();
        await SignInAsync(client, AuthorizeUrl());

        using var response = await client.GetAsync(
            AuthorizeUrl(prompt: PromptValues.None),
            TestContext.Current.CancellationToken);

        Assert.Contains("code=", AssertRedirectedToClient(response), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PromptLoginVuelveAMostrarLaPantallaDeIdentidadConSesionPreexistente()
    {
        using var client = Create();
        await SignInAsync(client, AuthorizeUrl());

        using var response = await client.GetAsync(
            AuthorizeUrl(prompt: PromptValues.Login),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"sub\"", await ReadBodyAsync(response), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PromptConsentMuestraLaPantallaDeConsentimiento()
    {
        using var client = Create();
        await SignInAsync(client, AuthorizeUrl());

        using var page = await client.GetAsync(
            AuthorizeUrl(prompt: PromptValues.Consent),
            TestContext.Current.CancellationToken);

        var html = await ReadBodyAsync(page);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("name=\"decision\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"password\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ElConsentimientoAceptadoRedirigeConCode()
    {
        using var client = Create();
        await SignInAsync(client, AuthorizeUrl());
        var fields = await ConsentFieldsAsync(client);

        using var response = await PostFormAsync(client, EndpointPaths.Authorize, Approved(fields));

        Assert.Contains("code=", AssertRedirectedToClient(response), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ElConsentimientoDenegadoRedirigeConAccessDenied()
    {
        using var client = Create();
        await SignInAsync(client, AuthorizeUrl());
        var fields = await ConsentFieldsAsync(client);

        using var response = await PostFormAsync(client, EndpointPaths.Authorize, Denied(fields));

        Assert.Contains("error=access_denied", AssertRedirectedToClient(response), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LaPantallaDeLoginMuestraElBrandingDelCliente()
    {
        using var client = Create();

        using var response = await client.GetAsync(AuthorizeUrl(), TestContext.Current.CancellationToken);
        var html = await ReadBodyAsync(response);

        Assert.Contains(HtmlEncoder.Default.Encode("OidcMock - Persona Física"), html, StringComparison.Ordinal);
        Assert.Contains("/assets/logo-mock.svg", html, StringComparison.Ordinal);
        Assert.Contains("#00695C", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LaPantallaDeIdentidadOfreceLosPerfilesDeUsersJsonYElSubjectEditable()
    {
        using var client = Create();

        using var response = await client.GetAsync(AuthorizeUrl(), TestContext.Current.CancellationToken);
        var html = await ReadBodyAsync(response);

        Assert.Contains("name=\"sub\"", html, StringComparison.Ordinal);
        Assert.Contains("jperez", html, StringComparison.Ordinal);
        Assert.Contains("empresa-demo", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"password\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ElPerfilElegidoDeterminaLaIdentidadDelCanje()
    {
        const string PerfilUserName = "prueba";
        const string PerfilSubject = "01-2222-3333";

        using var client = Create();
        using var loginPage = await client.GetAsync(
            $"{AuthorizeUrl()}&perfil={PerfilUserName}",
            TestContext.Current.CancellationToken);
        var fields = LoginFormFields.Parse(await ReadBodyAsync(loginPage));
        fields["action"] = "accept";

        using var granted = await PostFormAsync(client, EndpointPaths.Authorize, fields);
        var code = HttpUtility.ParseQueryString(AssertRedirectedToClient(granted))["code"]!;

        var tokens = await RequestTokensAsync(client, GrantTypes.AuthorizationCode, code);

        Assert.Equal(PerfilSubject, ReadJwtPayload(tokens.IdToken!).GetProperty("sub").GetString());
    }

    /// <summary>
    /// Recorre la pantalla de identidad simulando al navegador: GET al authorize, formulario
    /// precargado tal cual y POST al mismo endpoint. Devuelve la respuesta con la redireccion al
    /// cliente.
    /// </summary>
    private static async Task<HttpResponseMessage> SignInAsync(HttpClient client, string url)
    {
        using var loginPage = await client.GetAsync(url, TestContext.Current.CancellationToken);
        var fields = LoginFormFields.Parse(await ReadBodyAsync(loginPage));
        fields["action"] = "accept";

        return await PostFormAsync(client, EndpointPaths.Authorize, fields);
    }

    private static async Task<string> SignInForCodeAsync(HttpClient client)
    {
        using var response = await SignInAsync(client, AuthorizeUrl());

        return HttpUtility.ParseQueryString(AssertRedirectedToClient(response))["code"]!;
    }

    private static async Task<Dictionary<string, string>> ConsentFieldsAsync(HttpClient client)
    {
        using var page = await client.GetAsync(
            AuthorizeUrl(prompt: PromptValues.Consent),
            TestContext.Current.CancellationToken);

        return LoginFormFields.Parse(await ReadBodyAsync(page));
    }

    private static Dictionary<string, string> Approved(Dictionary<string, string> fields) =>
        Decision(fields, ConsentDecision.Allow);

    private static Dictionary<string, string> Denied(Dictionary<string, string> fields) =>
        Decision(fields, ConsentDecision.Deny);

    private static Dictionary<string, string> Decision(Dictionary<string, string> fields, string decision)
    {
        fields[ConsentFormFields.Decision] = decision;

        return fields;
    }

    private static async Task<HttpResponseMessage> PostTokenAsync(HttpClient client, string code)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = GrantTypes.AuthorizationCode,
            ["client_id"] = ClientId,
            ["redirect_uri"] = RedirectUri,
            ["code_verifier"] = CodeVerifier,
            ["code"] = code
        });

        return await client.PostAsync(
            $"{PathBase}/{EndpointPaths.Token}",
            form,
            TestContext.Current.CancellationToken);
    }

    private static readonly string DefaultCodeChallenge =
        OidcTestClient.Sha256Base64Url(OidcTestClient.CodeVerifier);

    private static string AuthorizeUrl(
        string? clientId = null,
        string? redirectUri = null,
        string? responseType = null,
        string? scope = null,
        bool withPkce = true,
        string? codeChallengeMethod = "S256",
        string? prompt = null,
        string? responseMode = null)
    {
        var parameters = new Dictionary<string, string?>();

        Add(parameters, "client_id", clientId ?? ClientId);
        Add(parameters, "redirect_uri", redirectUri ?? RedirectUri);
        Add(parameters, "response_type", responseType ?? "code");
        Add(parameters, "scope", scope ?? "openid email");
        Add(parameters, "state", "st-1");
        Add(parameters, "nonce", "n-1");
        if (withPkce)
        {
            Add(parameters, "code_challenge", DefaultCodeChallenge);
            Add(parameters, "code_challenge_method", codeChallengeMethod);
        }
        Add(parameters, "prompt", prompt);
        Add(parameters, "response_mode", responseMode);

        return OidcTestClient.BuildAuthorizeUrl(parameters);
    }

    private static void Add(Dictionary<string, string?> parameters, string name, string? value)
    {
        if (value is not null)
        {
            parameters[name] = value;
        }
    }

    private static string AssertRedirectedToClient(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        return response.Headers.Location!.ToString();
    }

    private static void AssertRedirectsWith(HttpResponseMessage response, string error)
    {
        var location = AssertRedirectedToClient(response);

        Assert.StartsWith(RedirectUri, location, StringComparison.Ordinal);
        Assert.Contains($"error={error}", location, StringComparison.Ordinal);
        Assert.Contains("state=st-1", location, StringComparison.Ordinal);
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
