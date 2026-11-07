namespace OidcMock.Host;

/// <summary>
/// Clave para autenticar las peticiones al endpoint de administracion del mock.
/// Si no se configura, se genera una al azar al arrancar (se imprime en el log).
/// </summary>
public sealed class AdminApiKeyOptions
{
    public string Key { get; set; } = string.Empty;
}