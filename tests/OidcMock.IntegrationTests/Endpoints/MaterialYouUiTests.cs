using OidcMock.Core.Authorization;
using OidcMock.Core.Discovery;
using static OidcMock.IntegrationTests.Endpoints.OidcTestClient;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// Contrato visual de las pantallas HTML del mock: todas llevan la hoja Material You dark de los
/// tokens de docs/UI-Prototype (color-scheme, superficies, acento, formas y escala tipografica), con
/// el color primario del branding del cliente mandando sobre el token del sistema.
/// </summary>
public sealed class MaterialYouUiTests
{
    private const string ColorSchemeDark = "color-scheme: dark";
    private const string SurfaceToken = "--md-surface:";
    private const string PrimaryToken = "--md-primary:";
    private const string ShapeToken = "--md-shape-xl:";
    private const string TypeToken = "--md-type-body:";
    private const string FormPostMode = "form_post";

    [Fact]
    public async Task LaPantallaDeIdentidadUsaLosTokensMaterialYou()
    {
        using var client = Create();

        using var response = await client.GetAsync(AuthorizeUrl(), TestContext.Current.CancellationToken);

        AssertMaterialYou(await ReadBodyAsync(response));
    }

    [Fact]
    public async Task LaPantallaDeAccountLoginUsaLosTokensMaterialYou()
    {
        using var client = Create();

        using var response = await client.GetAsync(
            $"{PathBase}/{EndpointPaths.AccountLogin}?ReturnUrl=/personafisica/",
            TestContext.Current.CancellationToken);

        AssertMaterialYou(await ReadBodyAsync(response));
    }

    [Fact]
    public async Task LaPantallaDeConsentimientoUsaLosTokensMaterialYou()
    {
        using var client = Create();
        await SignInAsync(client);

        var parameters = DefaultParameters();
        parameters["prompt"] = PromptValues.Consent;

        using var response = await client.GetAsync(
            BuildAuthorizeUrl(parameters),
            TestContext.Current.CancellationToken);

        AssertMaterialYou(await ReadBodyAsync(response));
    }

    [Fact]
    public async Task LaPaginaDeSesionCerradaUsaLosTokensMaterialYou()
    {
        using var client = Create();

        using var response = await client.GetAsync(
            $"{PathBase}/{EndpointPaths.EndSession}?client_id={ClientId}",
            TestContext.Current.CancellationToken);

        AssertMaterialYou(await ReadBodyAsync(response));
    }

    [Fact]
    public async Task LaPaginaDeFormPostUsaLosTokensMaterialYou()
    {
        using var client = Create();
        var parameters = DefaultParameters();
        parameters["response_mode"] = FormPostMode;

        using var loginPage = await client.GetAsync(BuildAuthorizeUrl(parameters), TestContext.Current.CancellationToken);
        var fields = LoginFormFields.Parse(await ReadBodyAsync(loginPage));
        fields["action"] = "accept";

        using var response = await PostFormAsync(client, EndpointPaths.Authorize, fields);

        AssertMaterialYou(await ReadBodyAsync(response));
    }

    [Fact]
    public async Task ElCheckSessionUsaLosTokensMaterialYou()
    {
        using var client = Create();

        using var response = await client.GetAsync(
            $"{PathBase}/{EndpointPaths.CheckSession}",
            TestContext.Current.CancellationToken);

        AssertMaterialYou(await ReadBodyAsync(response));
    }

    private static void AssertMaterialYou(string html)
    {
        Assert.Contains(ColorSchemeDark, html, StringComparison.Ordinal);
        Assert.Contains(SurfaceToken, html, StringComparison.Ordinal);
        Assert.Contains(PrimaryToken, html, StringComparison.Ordinal);
        Assert.Contains(ShapeToken, html, StringComparison.Ordinal);
        Assert.Contains(TypeToken, html, StringComparison.Ordinal);
    }

    private static Task<string> ReadBodyAsync(HttpResponseMessage response) =>
        response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}