using System.Net;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using OidcMock.Core.Crypto;
using OidcMock.Core.Discovery;
using OidcMock.Core.Grants;
using OidcMock.Host;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// Utilidades compartidas por las pruebas de los endpoints /connect/*.
/// </summary>
public static class OidcTestClient
{
    public const string PathBase = "/personafisica";
    public const string ClientId = "web-app-spa";
    public const string ServiceClientId = "backend-service";
    public const string ServiceSecret = "super-secret-backend";
    public const string RedirectUri = "https://localhost:5173/callback";
    public const string UserName = "jperez";
    public const string Password = "Passw0rd!";
    public const string CodeVerifier = "verificador-de-prueba-con-suficiente-longitud-0000000000000";

    /// <summary>
    /// Cliente que NO sigue redirecciones: el authorize responde con un 302 que hay que inspeccionar
    /// para leer el codigo, y un cliente que lo siguiera fallaria al intentar salir a la aplicacion.
    /// </summary>
    public static HttpClient Create() =>
        new WebApplicationFactory<Program>()
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public static string AuthorizeUrl() =>
        BuildAuthorizeUrl([]);

    /// <summary>
    /// URL de authorize con los parametros por defecto del cliente de pruebas, sobrescribiendo los
    /// que se pasen. Asi cada test declara solo lo que cambia respecto al caso bueno.
    /// </summary>
    public static string BuildAuthorizeUrl(Dictionary<string, string?> parameters) =>
        $"{PathBase}/{EndpointPaths.Authorize}?{QueryString(DefaultParameters(), parameters)}";

    private static IEnumerable<KeyValuePair<string, string?>> DefaultParameters() =>
    [
        new("client_id", ClientId),
        new("redirect_uri", RedirectUri),
        new("response_type", "code"),
        new("scope", "openid email"),
        new("state", "st-1"),
        new("nonce", "n-1"),
        new("code_challenge", Sha256Base64Url(CodeVerifier)),
        new("code_challenge_method", "S256")
    ];

    private static string QueryString(
        IEnumerable<KeyValuePair<string, string?>> defaults,
        Dictionary<string, string?> overrides)
    {
        var values = defaults
            .Where(parameter => !overrides.ContainsKey(parameter.Key))
            .Concat(overrides)
            .Select(parameter => $"{parameter.Key}={Uri.EscapeDataString(parameter.Value ?? string.Empty)}");

        return string.Join('&', values);
    }

    public static string Sha256Base64Url(string verifier) =>
        Base64Url.Encode(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.ASCII.GetBytes(verifier)));

    /// <summary>
    /// Recorre la pantalla de login del mock simulando el envio del formulario, y devuelve el codigo
    /// de autorizacion de la redireccion. Es el camino completo que haria un navegador.
    /// </summary>
    public static async Task<string> SignInAsync(HttpClient client)
    {
        using var loginPage = await client.GetAsync(AuthorizeUrl(), TestContext.Current.CancellationToken);
        var html = await loginPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var form = BuildSignInForm(html);
        using var response = await client.SendAsync(form, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return ReadCode(response.Headers.Location!);
    }

    public static async Task<IssuedTokens> RequestTokensAsync(
        HttpClient client,
        string grantType,
        string? code = null,
        IReadOnlyDictionary<string, string>? extra = null)
    {
        using var response = await client.PostAsync(
            $"{PathBase}/{EndpointPaths.Token}",
            BuildTokenForm(grantType, code, extra),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        return IssuedTokens.From(JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement);
    }

    public static Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string path,
        IReadOnlyDictionary<string, string> values) =>
        client.PostAsync(
            $"{PathBase}/{path}",
            new FormUrlEncodedContent(values),
            TestContext.Current.CancellationToken);

    private static HttpRequestMessage BuildSignInForm(string html)
    {
        var fields = LoginFormFields.Parse(html);
        fields["username"] = UserName;
        fields["password"] = Password;

        return new HttpRequestMessage(HttpMethod.Post, $"{PathBase}/{EndpointPaths.Authorize}")
        {
            Content = new FormUrlEncodedContent(fields)
        };
    }

    private static FormUrlEncodedContent BuildTokenForm(
        string grantType,
        string? code,
        IReadOnlyDictionary<string, string>? extra)
    {
        var values = new Dictionary<string, string>(extra ?? new Dictionary<string, string>())
        {
            ["grant_type"] = grantType,
            ["client_id"] = extra?.GetValueOrDefault("client_id") ?? ClientId,
            ["redirect_uri"] = RedirectUri,
            ["code_verifier"] = CodeVerifier
        };

        if (code is not null)
        {
            values["code"] = code;
        }

        return new FormUrlEncodedContent(values);
    }

    /// <summary>
    /// Lee el code de la redireccion trabajando sobre el texto del Location: con AllowAutoRedirect
    /// desactivado puede venir relativo, y Uri.Query lanza en ese caso.
    /// </summary>
    private static string ReadCode(Uri location)
    {
        var queryIndex = location.OriginalString.IndexOf('?', StringComparison.Ordinal);

        var code = queryIndex < 0
            ? null
            : HttpUtility.ParseQueryString(location.OriginalString[queryIndex..])["code"];

        return code ?? throw new InvalidOperationException(
            $"La redireccion a '{location}' no traia un codigo de autorizacion.");
    }
}

/// <summary>Tokens tal como los devuelve el token endpoint.</summary>
public sealed record IssuedTokens(string? AccessToken, string? IdToken, string? RefreshToken)
{
    public static IssuedTokens From(JsonElement body) => new(
        Read(body, "access_token"),
        Read(body, "id_token"),
        Read(body, "refresh_token"));

    private static string? Read(JsonElement body, string property) =>
        body.TryGetProperty(property, out var value) ? value.GetString() : null;
}