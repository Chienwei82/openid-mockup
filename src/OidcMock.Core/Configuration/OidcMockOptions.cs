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
}
