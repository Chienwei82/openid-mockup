using OidcMock.Core.Discovery;
using static OidcMock.IntegrationTests.Endpoints.OidcTestClient;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// Contrato de la pantalla de identidad: los campos (subject y claims editables) se ordenan en dos
/// columnas dentro de una tarjeta mas ancha, para que el formulario no obligue a bajar tanto. La
/// grilla vuelve a una columna en pantallas estrechas para no provocar scroll horizontal.
/// </summary>
public sealed class IdentityLayoutTests
{
    private const string FieldsContainer = "class=\"fields\"";
    private const string WideCard = "class=\"card wide\"";
    private const string TwoColumnRule = "grid-template-columns: repeat(2";
    private const string NarrowFallback = "@media (max-width: 34rem)";

    [Fact]
    public async Task LaPantallaDeIdentidadDelAuthorizeOrdenaLosCamposEnDosColumnas()
    {
        using var client = Create();

        using var response = await client.GetAsync(AuthorizeUrl(), TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains(WideCard, html, StringComparison.Ordinal);
        Assert.Contains(FieldsContainer, html, StringComparison.Ordinal);
        Assert.Contains(TwoColumnRule, html, StringComparison.Ordinal);
        Assert.Contains(NarrowFallback, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LaPantallaDeAccountLoginOrdenaLosCamposEnDosColumnas()
    {
        using var client = Create();

        using var response = await client.GetAsync(
            $"{PathBase}/{EndpointPaths.AccountLogin}?ReturnUrl=/personafisica/",
            TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains(WideCard, html, StringComparison.Ordinal);
        Assert.Contains(FieldsContainer, html, StringComparison.Ordinal);
        Assert.Contains(TwoColumnRule, html, StringComparison.Ordinal);
    }
}
