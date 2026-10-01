using OidcMock.Core.Configuration;
using OidcMock.Core.Discovery;

namespace OidcMock.UnitTests.Configuration;

public sealed class OidcMockOptionsTests
{
    [Fact]
    public void ElPathBasePorDefectoEsElDelServidorReal() =>
        Assert.Equal("/personafisica", new OidcMockOptions().PathBase);

    [Fact]
    public void SinIssuerConfiguradoElDocumentoUsaElHostDeLaPeticion() =>
        Assert.Null(new OidcMockOptions().Issuer);

    [Theory]
    [InlineData("personafisica", "/personafisica")]
    [InlineData("/personafisica/", "/personafisica")]
    [InlineData("/otro-prefijo", "/otro-prefijo")]
    [InlineData("", "/")]
    [InlineData("/", "/")]
    public void NormalizaElPathBaseConUnaSolaBarraInicialYNingunaFinal(string pathBase, string expected) =>
        Assert.Equal(expected, EndpointUri.NormalizePathBase(pathBase));

    [Theory]
    [InlineData("https://oauth2.bccr.fi.cr/personafisica", "https://oauth2.bccr.fi.cr/personafisica/")]
    [InlineData("https://oauth2.bccr.fi.cr/personafisica/", "https://oauth2.bccr.fi.cr/personafisica/")]
    [InlineData("http://localhost:5000/personafisica", "http://localhost:5000/personafisica/")]
    public void NormalizaElIssuerConBarraFinalComoElServidorReal(string issuer, string expected) =>
        Assert.Equal(expected, EndpointUri.NormalizeIssuer(issuer));

    [Fact]
    public void CombinaElIssuerConUnaRutaRelativaSinDuplicarBarras() =>
        Assert.Equal(
            "https://oauth2.bccr.fi.cr/personafisica/connect/token",
            EndpointUri.Combine("https://oauth2.bccr.fi.cr/personafisica/", "/connect/token"));
}
