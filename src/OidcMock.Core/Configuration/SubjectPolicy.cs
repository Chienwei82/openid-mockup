namespace OidcMock.Core.Configuration;

/// <summary>
/// Politica para decidir el <c>sub</c> por defecto cuando no hay un usuario autenticado detras
/// del token (por ejemplo en <c>client_credentials</c>).
/// </summary>
public enum SubjectPolicy
{
    /// <summary>El <c>sub</c> es el <c>client_id</c> (comportamiento actual).</summary>
    ClientId,

    /// <summary>El <c>sub</c> es la direccion IP del cliente que hace la peticion.</summary>
    IpAddress,

    /// <summary>Solo se emite token si hay un usuario autenticado; de lo contrario error.</summary>
    User,
}