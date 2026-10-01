namespace OidcMock.IntegrationTests;

/// <summary>
/// Crea un directorio temporal con un unico archivo de configuracion, para poder mutarlo en cada prueba.
/// </summary>
public sealed class TempConfigDirectory : IDisposable
{
    private const string ClientsFileName = "clients.json";
    private const string UsersFileName = "users.json";
    private const string ScopesFileName = "scopes.json";

    public TempConfigDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"oidc-mock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string WriteFile(string fileName, string content)
    {
        var filePath = System.IO.Path.Combine(Path, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    public string FilePath(string fileName) => System.IO.Path.Combine(Path, fileName);

    public string WriteClientsFile(string content) => WriteFile(ClientsFileName, content);

    public string WriteUsersFile(string content) => WriteFile(UsersFileName, content);

    public string WriteScopesFile(string content) => WriteFile(ScopesFileName, content);

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
