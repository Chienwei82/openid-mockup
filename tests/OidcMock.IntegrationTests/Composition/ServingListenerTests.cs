using Microsoft.Extensions.Configuration;
using OidcMock.Core.Configuration;
using OidcMock.Host;

namespace OidcMock.IntegrationTests.Composition;

/// <summary>
/// El mock escucha por Kestrel solo cuando no lo hospeda IIS: bajo IIS el enlace y el puerto los pone
/// el modulo de ASP.NET Core (ANCM) y fijarlos a mano dejaria al modulo sin alcanzar la app.
/// </summary>
public sealed class ServingListenerTests
{
    [Fact]
    public void ConfiguraKestrelCuandoNoLoHospedaIisYHayAlgunEsquemaEncendido()
    {
        var serving = new ServingOptions { UseHttps = true, AllowHttp = false };

        Assert.True(ServingListener.ShouldConfigureKestrel(serving, hostedByIis: false));
    }

    [Fact]
    public void NoConfiguraKestrelBajoIisPorqueElPuertoLoDaElModuloDeIis()
    {
        var serving = new ServingOptions { UseHttps = true, AllowHttp = false };

        Assert.False(ServingListener.ShouldConfigureKestrel(serving, hostedByIis: true));
    }

    [Fact]
    public void NoConfiguraKestrelSiNingunEsquemaEstaEncendido()
    {
        var serving = new ServingOptions { UseHttps = false, AllowHttp = false };

        Assert.False(ServingListener.ShouldConfigureKestrel(serving, hostedByIis: false));
    }

    [Theory]
    [InlineData("ASPNETCORE_PORT")]
    [InlineData("ASPNETCORE_IIS_PHYSICAL_PATH")]
    public void DetectaElHostingEnIisPorLasVariablesDelModulo(string variable)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [variable] = "1" })
            .Build();

        Assert.True(ServingListener.IsHostedByIis(configuration));
    }

    [Fact]
    public void SinVariablesDeIisNoSeConsideraHospedadoEnIis()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.False(ServingListener.IsHostedByIis(configuration));
    }
}
