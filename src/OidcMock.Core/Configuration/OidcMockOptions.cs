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
    /// Origenes de desarrollo local admitidos por defecto: los puertos tipicos de un servidor de
    /// desarrollo de SPA (Angular, Vite, React). CORS solo responde a estos, nunca con comodin.
    /// </summary>
    public static IReadOnlyList<string> DefaultAllowedCorsOrigins { get; } =
    [
        "http://localhost:4200",
        "http://localhost:5173",
        "http://localhost:3000"
    ];

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

    /// <summary>
    /// Origenes de navegador a los que se responde con cabeceras CORS. El discovery, el JWKS, el
    /// token endpoint y el userinfo los consumen SPAs que se sirven desde otro origen. Viene vacia
    /// para que el enlace de configuracion la reemplace entera; si sigue vacia, el host aplica
    /// <see cref="DefaultAllowedCorsOrigins"/>.
    /// </summary>
    public IReadOnlyList<string> AllowedCorsOrigins { get; set; } = [];

    /// <summary>Puertos y TLS con los que escucha el mock.</summary>
    public ServingOptions Serving { get; init; } = new();
}
