namespace OidcMock.Host.Configuration;

/// <summary>
/// Opciones del endpoint de administracion: la clave para autenticar las peticiones de
/// escritura (X-Admin-Key header). Si no se configura, se genera una al azar al arrancar.
/// </summary>
public sealed class AdminOptions
{
    public AdminApiKeyOptions ApiKey { get; set; } = new();
}