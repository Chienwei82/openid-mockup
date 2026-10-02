namespace OidcMock.Core.Configuration;

/// <summary>
/// Como escucha el mock: HTTPS con el certificado de desarrollo (por defecto, igual que
/// <c>dotnet dev-certs</c>) o HTTP plano, que es lo que se usa en contenedores.
/// </summary>
public sealed class ServingOptions
{
    /// <summary>Puerto por defecto de HTTPS, el habitual del perfil de desarrollo de ASP.NET Core.</summary>
    public const int DefaultHttpsPort = 5443;

    /// <summary>Puerto por defecto de HTTP plano, el habitual dentro de un contenedor.</summary>
    public const int DefaultHttpPort = 8080;

    /// <summary>Puerto de HTTPS. Se ignora si <see cref="UseHttps"/> es false.</summary>
    public int HttpsPort { get; init; } = DefaultHttpsPort;

    /// <summary>Puerto de HTTP plano. Se ignora si <see cref="AllowHttp"/> es false.</summary>
    public int HttpPort { get; init; } = DefaultHttpPort;

    /// <summary>
    /// Cuando es false el mock no escucha HTTPS. Pensado para contenedores y para setups donde el
    /// TLS lo termina un proxy delante.
    /// </summary>
    public bool UseHttps { get; init; } = true;

    /// <summary>
    /// Cuando es true el mock tambien escucha en HTTP plano, para clientes que no aceptan
    /// certificados de desarrollo.
    /// </summary>
    public bool AllowHttp { get; init; }

    /// <summary>
    /// Ruta de un PFX propio. Si es null y <see cref="UseHttps"/> es true se usa el certificado de
    /// desarrollo del machine.
    /// </summary>
    public string? CertificatePath { get; init; }

    /// <summary>Contraseña del PFX de <see cref="CertificatePath"/>, si tiene.</summary>
    public string? CertificatePassword { get; init; }
}