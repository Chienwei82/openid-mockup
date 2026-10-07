using System.Net;
using OidcMock.Core.Authorization;
using OidcMock.Core.Discovery;
using static OidcMock.IntegrationTests.Endpoints.OidcTestClient;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// La pantalla de identidad del mock: sin usuario ni contrasena (autenticar de verdad no aporta
/// nada a un mockup), se escribe el subject, se ajustan los valores que iran en el JWT y se acepta
/// o se deniega. Aceptar lleva al redirect_uri del cliente; Denegar simula un fallo de
/// autenticacion, que el cliente ve como error=access_denied (RFC 6749 4.1.2.1).
/// </summary>
public sealed class IdentityFormEndpointTests
{
    private const string SubjectField = "sub";
    private const string ClaimPrefix = "claim.";
    private const string ActionField = "action";
    private const string Accept = "accept";
    private const string Deny = "deny";
    private const string PerfilField = "perfil";
    private const string PerfilUserName = "prueba";
    private const string PerfilSubject = "01-2222-3333";
    private const string DefaultSubject = "user-persona-fisica";
    private const string HybridScopes = "openid email profile";

    [Fact]
    public async Task LaPantallaPideSubjectYClaimsEditablesSinPedirContrasena()
    {
        using var client = Create();

        using var response = await client.GetAsync(AuthorizeUrl(), TestContext.Current.CancellationToken);
        var html = await ReadBodyAsync(response);

        Assert.Contains($"name=\"{SubjectField}\"", html, StringComparison.Ordinal);
        Assert.Contains($"name=\"{ClaimPrefix}given_name\"", html, StringComparison.Ordinal);
        Assert.Contains($"name=\"{ClaimPrefix}email\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"password\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("type=\"password\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LosCamposLlevanValoresPorDefectoParaAceptarDirectamente()
    {
        using var client = Create();

        using var response = await client.GetAsync(AuthorizeUrl(), TestContext.Current.CancellationToken);
        var html = await ReadBodyAsync(response);

        Assert.Contains($"value=\"{DefaultSubject}\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"Juan\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"jperez@example.cr\"", html, StringComparison.Ordinal);
        Assert.Contains($"value=\"{Accept}\"", html, StringComparison.Ordinal);
        Assert.Contains($"value=\"{Deny}\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AceptarConLosValoresPorDefectoLlevaAlRedirectUriDelCliente()
    {
        using var client = Create();
        var fields = await FormFieldsAsync(client, AuthorizeUrl());
        fields[ActionField] = Accept;

        using var response = await PostFormAsync(client, EndpointPaths.Authorize, fields);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("code=", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LosValoresEditadosDelFormularioAparecenEnElIdToken()
    {
        const string EditedSubject = "01-9999-8888";
        using var client = Create();
        var fields = await FormFieldsAsync(client, HybridAuthorizeUrl());
        fields[SubjectField] = EditedSubject;
        fields[$"{ClaimPrefix}name"] = "Nombre Editado";
        fields[$"{ClaimPrefix}email"] = "editado@example.cr";
        fields[ActionField] = Accept;

        using var response = await PostFormAsync(client, EndpointPaths.Authorize, fields);

        var idToken = ReadJwtPayload(FragmentValue(response.Headers.Location!, "id_token"));
        Assert.Equal(EditedSubject, idToken.GetProperty("sub").GetString());
        Assert.Equal("Nombre Editado", idToken.GetProperty("name").GetString());
        Assert.Equal("editado@example.cr", idToken.GetProperty("email").GetString());
    }

    [Fact]
    public async Task DenegarSimulaUnFalloDeAutenticacion()
    {
        using var client = Create();
        var fields = await FormFieldsAsync(client, AuthorizeUrl());
        fields[ActionField] = Deny;

        using var response = await PostFormAsync(client, EndpointPaths.Authorize, fields);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("error=access_denied", location, StringComparison.Ordinal);
        Assert.Contains("state=st-1", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ElPerfilDeUsersJsonRecargaLosValoresDeEseUsuario()
    {
        using var client = Create();

        using var response = await client.GetAsync(
            $"{AuthorizeUrl()}&{PerfilField}={PerfilUserName}",
            TestContext.Current.CancellationToken);
        var html = await ReadBodyAsync(response);

        Assert.Contains($"value=\"{PerfilSubject}\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"Usuario\"", html, StringComparison.Ordinal);
    }

    private static async Task<Dictionary<string, string>> FormFieldsAsync(HttpClient client, string url)
    {
        using var page = await client.GetAsync(url, TestContext.Current.CancellationToken);
        return LoginFormFields.Parse(await ReadBodyAsync(page));
    }

    private static string HybridAuthorizeUrl()
    {
        var parameters = DefaultParameters();
        parameters["response_type"] = ResponseTypeNames.CodeIdToken;
        parameters["scope"] = HybridScopes;
        return BuildAuthorizeUrl(parameters);
    }

    private static string FragmentValue(Uri location, string name)
    {
        foreach (var pair in location.Fragment.TrimStart('#').Split('&'))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0] == name)
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        throw new InvalidOperationException($"La respuesta no traia {name} en el fragmento: {location}");
    }

    private static Task<string> ReadBodyAsync(HttpResponseMessage response) =>
        response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
