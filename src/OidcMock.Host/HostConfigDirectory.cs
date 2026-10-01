using OidcMock.Core.Configuration;

namespace OidcMock.Host;

/// <summary>
/// Localiza el directorio 'config' tanto al ejecutar con 'dotnet run' como desde la carpeta de publicacion.
/// </summary>
public static class HostConfigDirectory
{
    private const string ConfigDirectoryName = "config";
    private const string RepositoryRelativePath = "../..";

    /// <summary>
    /// Clave de configuracion que permite forzar el directorio de configuracion (pruebas, despliegues).
    /// </summary>
    public const string ConfigDirectorySettingName = "OidcMock:ConfigDirectory";

    /// <summary>
    /// Clave de configuracion que permite desactivar la recarga en caliente.
    /// </summary>
    public const string ReloadOnChangeSettingName = "OidcMock:ReloadOnChange";

    public static string Resolve(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return CandidateDirectories(environment)
            .FirstOrDefault(Directory.Exists) ?? Path.Combine(environment.ContentRootPath, ConfigDirectoryName);
    }

    public static JsonStoreOptions DefaultOptions(IHostEnvironment environment, bool reloadOnChange = true) => new()
    {
        ConfigDirectory = Resolve(environment),
        ReloadOnChange = reloadOnChange
    };

    /// <summary>
    /// Prioriza el config/ del repositorio para que editarlo afecte al ejecutar con 'dotnet run'.
    /// </summary>
    private static IEnumerable<string> CandidateDirectories(IHostEnvironment environment)
    {
        yield return Path.Combine(environment.ContentRootPath, ConfigDirectoryName);
        yield return Path.Combine(environment.ContentRootPath, RepositoryRelativePath, ConfigDirectoryName);
        yield return Path.Combine(AppContext.BaseDirectory, ConfigDirectoryName);
    }
}
