namespace OidcMock.Core.Network;

/// <summary>
/// Resuelve la direccion IP del cliente que hace la peticion HTTP actual.
/// Vive en Core (sin ASP.NET) para poder probarse como dominio puro; el adaptador
/// que lee HttpContext vive en Host.
/// </summary>
public interface IIpResolver
{
    /// <summary>Devuelve la IP remota del llamante, o null si no se puede determinar.</summary>
    string? Resolve();
}