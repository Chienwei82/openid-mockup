namespace OidcMock.Core.Codes;

/// <summary>
/// Genera los valores opacos que el mock usa como codigo de autorizacion y refresh token.
/// </summary>
public static class OpaqueToken
{
    private const int TokenByteLength = 32;
    private const int CodeByteLength = 32;
    private const string CodeSuffix = "-1";

    /// <summary>
    /// Valor opaco en base64url con 256 bits de entropia, suficiente para un mock offline: no es un
    /// secreto de produccion, pero evita codes adivinables y colisiones en las pruebas.
    /// </summary>
    public static string New() => Crypto.Base64Url.Encode(
        System.Security.Cryptography.RandomNumberGenerator.GetBytes(TokenByteLength));

    /// <summary>
    /// El codigo de autorizacion en el formato observable del servidor real: 32 bytes en hexadecimal
    /// mayuscula con el sufijo -1. A diferencia de los demas valores, su forma es visible en la URL
    /// de retorno y el mock la imita para que la sustitucion del servidor real no se note.
    /// </summary>
    public static string NewAuthorizationCode() =>
        $"{Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(CodeByteLength))}{CodeSuffix}";
}