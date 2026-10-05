namespace OidcMock.Core.Codes;

/// <summary>
/// Metodos de transformacion del code challenge de PKCE (RFC 7636) que el mock acepta.
/// </summary>
public static class PkceCodeChallengeMethods
{
    public const string Plain = "plain";
    public const string Sha256 = "S256";

    public static readonly string[] Supported = [Plain, Sha256];
}