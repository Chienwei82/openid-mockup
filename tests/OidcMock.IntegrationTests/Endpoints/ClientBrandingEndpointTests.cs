using System.Net;
using OidcMock.Core.Authorization;
using static OidcMock.IntegrationTests.Endpoints.OidcTestClient;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// El branding visible del cliente de prueba: la pantalla de login del mock muestra el nombre con el
/// que ese cliente se identifica. Vive en config/clients.json, pero es contrato observable: quien
/// sustituye al servidor real espera su nombre en pantalla.
/// </summary>
public sealed class ClientBrandingEndpointTests
{
    private const string TestClientId = "fb02079c-3143-49e6-a776-dd9b002388d2";
    private const string TestClientRedirectUri = "http://localhost:5173/pruebas/ves/landing";
    private const string ExpectedDisplayName = "OidcMock - Pruebas Open ID";
    private const string PreviousDisplayName = "Prueba GAUDI";

    [Fact]
    public async Task ElClienteDePruebaSeLlamaPruebasOpenIdEnSuPantalla()
    {
        using var client = Create();

        using var response = await client.GetAsync(AuthorizeUrlForTestClient(), TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(ExpectedDisplayName, html, StringComparison.Ordinal);
        Assert.DoesNotContain(PreviousDisplayName, html, StringComparison.Ordinal);
    }

    private static string AuthorizeUrlForTestClient()
    {
        var parameters = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["client_id"] = TestClientId,
            ["redirect_uri"] = TestClientRedirectUri,
            ["response_type"] = ResponseTypeNames.Code,
            ["scope"] = "openid profile email offline_access",
            ["state"] = "st-1",
            ["nonce"] = "n-1",
            ["code_challenge"] = Sha256Base64Url(CodeVerifier),
            ["code_challenge_method"] = "S256"
        };

        return BuildAuthorizeUrl(parameters);
    }
}
