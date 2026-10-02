namespace OidcMock.Core.Errors;

/// <summary>
/// El reto de autenticacion de un recurso protegido por tokens (RFC 6750 3), que es lo que viaja en el
/// encabezado WWW-Authenticate de un 401. Vive en Core porque su forma es parte del protocolo, no del
/// host, y asi se puede comprobar en tests de dominio.
/// </summary>
public static class BearerChallenge
{
    public const string Scheme = "Bearer";

    public const string InvalidTokenError = "invalid_token";

    /// <summary>
    /// RFC 6750 3.1: si la peticion no trae credenciales, el reto va **sin** codigo de error, porque no
    /// se puede decir que el token sea invalido cuando no se presento ninguno.
    /// </summary>
    public static string WithoutError() => Scheme;

    /// <summary>
    /// Reto con el codigo de error y su descripcion, que es lo que un cliente necesita para saber si
    /// debe pedir un token nuevo o simplemente ha caducado el suyo.
    /// </summary>
    public static string ForInvalidToken(string description) =>
        $"{Scheme} error=\"{InvalidTokenError}\", error_description=\"{Quote(description)}\"";

    private static string Quote(string value) => value.Replace("\"", "'", StringComparison.Ordinal);
}