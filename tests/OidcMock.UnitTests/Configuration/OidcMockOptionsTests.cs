using OidcMock.Core.Configuration;
using OidcMock.Core.Discovery;

namespace OidcMock.UnitTests.Configuration;

public sealed class OidcMockOptionsValidatorTests
{
    [Fact]
    public void LasOpcionesPorDefectoSonValidas()
    {
        var errors = OidcMockOptionsValidator.Validate(new OidcMockOptions());

        Assert.Empty(errors);
    }

    [Fact]
    public void SinIssuerConfiguradoNoHayErrorPorqueSeDeduceDelHost()
    {
        var errors = OidcMockOptionsValidator.Validate(new OidcMockOptions { Issuer = null });

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("personafisica")]
    [InlineData("")]
    [InlineData("   ")]
    public void RechazaUnPathBaseInvalido(string pathBase)
    {
        var errors = OidcMockOptionsValidator.Validate(new OidcMockOptions { PathBase = pathBase });

        Assert.Contains(errors, error => error.Setting == "OidcMock:PathBase");
    }

    [Theory]
    [InlineData("no-es-una-url")]
    [InlineData("ftp://bccr.fi.cr/personafisica")]
    public void RechazaUnIssuerQueNoEsUnaUrlHttp(string issuer)
    {
        var errors = OidcMockOptionsValidator.Validate(new OidcMockOptions { Issuer = issuer });

        Assert.Contains(errors, error => error.Setting == "OidcMock:Issuer");
    }

    [Fact]
    public void AceptaUnIssuerHttpAbsoluto()
    {
        var errors = OidcMockOptionsValidator.Validate(new OidcMockOptions { Issuer = "http://localhost:5000/personafisica" });

        Assert.Empty(errors);
    }

    [Fact]
    public void AceptaUnBaseUrlAbsolutoHttp()
    {
        var errors = OidcMockOptionsValidator.Validate(new OidcMockOptions { BaseUrl = "https://miserver/oidc" });

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("no-es-una-url")]
    [InlineData("ftp://miserver/oidc")]
    [InlineData("https://miserver/oidc?x=1")]
    public void RechazaUnBaseUrlQueNoEsUnaUrlHttpSinQueryNiFragmento(string baseUrl)
    {
        var errors = OidcMockOptionsValidator.Validate(new OidcMockOptions { BaseUrl = baseUrl });

        Assert.Contains(errors, error => error.Setting == "OidcMock:BaseUrl");
    }

    [Fact]
    public void RechazaBaseUrlJuntoConIssuerPorqueBaseUrlYaFijaElEmisor()
    {
        var options = new OidcMockOptions
        {
            BaseUrl = "https://miserver/oidc",
            Issuer = "https://otro.example/oidc"
        };

        var errors = OidcMockOptionsValidator.Validate(options);

        Assert.Contains(errors, error => error.Setting == "OidcMock:BaseUrl");
    }

    [Fact]
    public void RechazaUnaVigenciaDeSesionNoPositiva()
    {
        var errors = OidcMockOptionsValidator.Validate(new OidcMockOptions { SessionLifetime = TimeSpan.Zero });

        Assert.Contains(errors, error => error.Setting == "OidcMock:SessionLifetime");
    }

    [Theory]
    [InlineData("http://localhost:4200")]
    [InlineData("https://spa.example.com")]
    [InlineData("http://192.168.1.10:5173")]
    public void AceptaOriginesCorsSinRuta(string origin)
    {
        var errors = OidcMockOptionsValidator.Validate(new OidcMockOptions { AllowedCorsOrigins = [origin] });

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("http://localhost:4200/app")]
    [InlineData("localhost:4200")]
    [InlineData("")]
    public void RechazaOriginesCorsQueNoSonUnOrigen(string origin)
    {
        var errors = OidcMockOptionsValidator.Validate(new OidcMockOptions { AllowedCorsOrigins = [origin] });

        Assert.Contains(errors, error => error.Setting == "OidcMock:AllowedCorsOrigins:0");
    }

    [Fact]
    public void ReportaTodosLosProblemasJuntosYNoSoloElPrimero()
    {
        var options = new OidcMockOptions
        {
            PathBase = "personafisica",
            SessionLifetime = TimeSpan.Zero,
            AllowedCorsOrigins = ["*"]
        };

        var errors = OidcMockOptionsValidator.Validate(options);

        Assert.Equal(3, errors.Count);
    }

    [Fact]
    public void AceptaHttpPlanoSinHttpsParaContenedores()
    {
        var options = new OidcMockOptions
        {
            Serving = new ServingOptions { UseHttps = false, AllowHttp = true, HttpPort = 8080 }
        };

        var errors = OidcMockOptionsValidator.Validate(options);

        Assert.Empty(errors);
    }

    [Fact]
    public void RechazaNoEscucharEnNingunEsquema()
    {
        var options = new OidcMockOptions
        {
            Serving = new ServingOptions { UseHttps = false, AllowHttp = false }
        };

        var errors = OidcMockOptionsValidator.Validate(options);

        Assert.Contains(errors, error => error.Setting == "OidcMock:Serving");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(70000)]
    public void RechazaUnPuertoHttpsFueraDeRango(int port)
    {
        var options = new OidcMockOptions { Serving = new ServingOptions { HttpsPort = port } };

        var errors = OidcMockOptionsValidator.Validate(options);

        Assert.Contains(errors, error => error.Setting == "OidcMock:Serving:HttpsPort");
    }

    [Fact]
    public void NoValidaElPuertoDelEsquemaQueEstaApagado()
    {
        var options = new OidcMockOptions
        {
            Serving = new ServingOptions { UseHttps = true, AllowHttp = false, HttpPort = 0 }
        };

        var errors = OidcMockOptionsValidator.Validate(options);

        Assert.Empty(errors);
    }
}

public sealed class OidcMockOptionsTests
{
    [Fact]
    public void ElPathBasePorDefectoEsElDelServidorReal() =>
        Assert.Equal("/personafisica", new OidcMockOptions().PathBase);

    [Fact]
    public void SinIssuerConfiguradoElDocumentoUsaElHostDeLaPeticion() =>
        Assert.Null(new OidcMockOptions().Issuer);

    [Fact]
    public void ElBaseUrlFijaElPathBaseYElEmisor()
    {
        var options = new OidcMockOptions { BaseUrl = "https://miserver/oidc" };

        Assert.Equal("/oidc", EndpointUri.NormalizePathBase(options.EffectivePathBase));
        Assert.Equal("https://miserver/oidc", options.EffectiveIssuer);
    }

    [Fact]
    public void ElBaseUrlMandaSobreElPathBase()
    {
        var options = new OidcMockOptions { BaseUrl = "https://miserver/openid", PathBase = "/personafisica" };

        Assert.Equal("/openid", EndpointUri.NormalizePathBase(options.EffectivePathBase));
    }

    [Fact]
    public void SinBaseUrlSeMantienenElPathBaseYElEmisorDeSiempre()
    {
        var options = new OidcMockOptions { PathBase = "/personafisica" };

        Assert.Equal("/personafisica", EndpointUri.NormalizePathBase(options.EffectivePathBase));
        Assert.Null(options.EffectiveIssuer);
    }

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
