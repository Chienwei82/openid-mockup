namespace OidcMock.IntegrationTests;

/// <summary>
/// Localiza la raiz del repositorio para leer los archivos de configuracion de ejemplo.
/// </summary>
public static class RepositoryLayout
{
    private const string SolutionFileName = "OidcMock.slnx";
    private const string ConfigDirectoryName = "config";

    public static string RootDirectory { get; } = FindRootDirectory();

    public static string ConfigDirectory => Path.Combine(RootDirectory, ConfigDirectoryName);

    /// <summary>Proyecto del host: de ahi se lee el contrato de publicacion.</summary>
    public static string HostProjectFile => Path.Combine(RootDirectory, "src", "OidcMock.Host", "OidcMock.Host.csproj");

    /// <summary>
    /// Content root del host del mock en las pruebas. Fijarlo evita que WebApplicationFactory infiera
    /// el directorio de contenido y monte un watcher para detectarlo.
    /// </summary>
    public static string ContentRoot => Path.Combine(RootDirectory, "src", "OidcMock.Host");

    public static string ConfigFile(string fileName) => Path.Combine(ConfigDirectory, fileName);

    public static void EnsureExists(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No se encontro el archivo de configuracion esperado: {path}", path);
        }
    }

    private static string FindRootDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException($"No se localizo {SolutionFileName} a partir de {AppContext.BaseDirectory}.");
    }
}
