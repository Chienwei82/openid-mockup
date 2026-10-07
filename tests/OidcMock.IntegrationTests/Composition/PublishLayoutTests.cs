using System.Xml.Linq;

namespace OidcMock.IntegrationTests.Composition;

/// <summary>
/// Contrato de la publicacion autocontenida: la configuracion viaja como archivos reales junto al
/// ejecutable (editable y montable), no empaquetada dentro del binario de un solo archivo. Si algo
/// vuelve a empaquetarla, el publicado no arranca con su config/ al lado y nadie se entera hasta
/// ejecutar el binario lejos del repositorio: aqui se comprueba sin publicar.
/// </summary>
public sealed class PublishLayoutTests
{
    [Fact]
    public void LaConfiguracionSePublicaComoArchivosRealesJuntoAlEjecutable()
    {
        var project = XDocument.Load(RepositoryLayout.HostProjectFile);

        var configContent = project
            .Descendants("Content")
            .Where(content => (string?)content.Attribute("Include") is { } include && include.Contains("config", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(configContent);
        Assert.All(
            configContent,
            content => Assert.Equal("true", (string?)content.Attribute("ExcludeFromSingleFile")));
    }

    [Fact]
    public void ElBinarioNoEmpaquetaContenidoParaExtraerloEnEjecucion()
    {
        var project = XDocument.Load(RepositoryLayout.HostProjectFile);

        // Con IncludeAllContentForSelfExtract el contenido se extrae a un directorio temporal y
        // AppContext.BaseDirectory deja de ser la carpeta del ejecutable: el config/ al lado del
        // binario no se encontraria.
        Assert.DoesNotContain(
            project.Descendants("IncludeAllContentForSelfExtract"),
            element => string.Equals((string?)element, "true", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LaConfiguracionDeAppsettingsSePublicaComoArchivoRealJuntoAlEjecutable()
    {
        var project = XDocument.Load(RepositoryLayout.HostProjectFile);

        var appsettings = project
            .Descendants("Content")
            .Where(content => (string?)content.Attribute("Update") == "appsettings.json")
            .ToList();

        Assert.NotEmpty(appsettings);
        Assert.All(
            appsettings,
            content => Assert.Equal("true", (string?)content.Attribute("ExcludeFromSingleFile")));
    }
}