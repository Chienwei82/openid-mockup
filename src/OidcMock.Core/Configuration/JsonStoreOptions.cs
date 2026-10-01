namespace OidcMock.Core.Configuration;

/// <summary>
/// Opciones de lectura de los archivos JSON de configuracion (Options pattern).
/// </summary>
public sealed class JsonStoreOptions
{
    /// <summary>
    /// Directorio que contiene clients.json, users.json y scopes.json.
    /// </summary>
    public required string ConfigDirectory { get; init; }

    /// <summary>
    /// Cuando es true, los archivos se vuelven a leer en cada acceso si cambiaron en disco.
    /// </summary>
    public bool ReloadOnChange { get; init; } = true;
}
