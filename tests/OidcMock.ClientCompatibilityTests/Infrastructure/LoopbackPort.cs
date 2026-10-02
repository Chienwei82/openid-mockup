using System.Net;
using System.Net.Sockets;

namespace OidcMock.ClientCompatibilityTests.Infrastructure;

/// <summary>
/// Localiza la raiz del repositorio para leer el <c>config/</c> de ejemplo, igual que hace el
/// proyecto de integracion. No se referencia aquel proyecto: los proyectos de test no se referencian
/// entre si, cada uno con su copia de la pieza que necesita.
/// </summary>
public static class RepositoryLayout
{
    private const string SolutionFileName = "OidcMock.slnx";
    private const string ConfigDirectoryName = "config";

    public static string RootDirectory { get; } = FindRootDirectory();

    public static string ConfigDirectory => Path.Combine(RootDirectory, ConfigDirectoryName);

    public static string ConfigFile(string fileName) => Path.Combine(ConfigDirectory, fileName);

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

/// <summary>
/// Rutas del mock tal como las anuncia su discovery. El PathBase por defecto es el del servidor real.
/// </summary>
public static class MockEndpoints
{
    public const string PathBase = "/personafisica";

    public const string Authorize = "connect/authorize";

    public const string Token = "connect/token";

    public const string UserInfo = "connect/userinfo";

    public const string EndSession = "connect/endsession";

    public const string Discovery = ".well-known/openid-configuration";

    public const string Jwks = ".well-known/openid-configuration/jwks";

    public const string PushedAuthorizationRequest = "connect/par";

    /// <summary>Combina el PathBase con una ruta del discovery, sin duplicar barras.</summary>
    public static string Resolve(string relative) => $"{PathBase}/{relative.TrimStart('/')}";
}