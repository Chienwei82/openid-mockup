namespace OidcMock.Core.Configuration;

/// <summary>
/// Opciones de comportamiento del mock: prefijo de rutas y emisor anunciado en el discovery.
/// </summary>
public sealed class OidcMockOptions
{
    /// <summary>
    /// Prefijo por defecto de todas las rutas del mock, igual que en el servidor real.
    /// </summary>
    public const string DefaultPathBase = "/personafisica";

    /// <summary>
    /// Prefijo bajo el que se publican el discovery y los endpoints. Se normaliza con
    /// <see cref="Discovery.EndpointUri.NormalizePathBase"/>.
    /// </summary>
    public string PathBase { get; init; } = DefaultPathBase;

    /// <summary>
    /// Emisor anunciado. Cuando es null se deduce del host de la peticion, de modo que el mock
    /// sirve un discovery coherente sin configuracion previa.
    /// </summary>
    public string? Issuer { get; init; }

    /// <summary>
    /// Vigencia de la sesion de login que recuerda el authorize entre peticiones. Es larga a
    /// proposito: en un mock de desarrollo interesa no reescribir la contrasena cada vez.
    /// </summary>
    public static TimeSpan DefaultSessionLifetime => TimeSpan.FromHours(8);

    /// <summary>Vigencia de la sesion de login que recuerda el authorize entre peticiones.</summary>
    public TimeSpan SessionLifetime { get; init; } = DefaultSessionLifetime;
}
