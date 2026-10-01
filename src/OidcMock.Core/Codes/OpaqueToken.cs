namespace OidcMock.Core.Codes;

/// <summary>
/// Genera los valores opacos que el mock usa como codigo de autorizacion y refresh token.
/// </summary>
public static class OpaqueToken
{
    private const int TokenByteLength = 32;

    /// <summary>
    /// Valor opaco en base64url con 256 bits de entropia, suficiente para un mock offline: no es un
    /// secreto de produccion, pero evita codes adivinables y colisiones en las pruebas.
    /// </summary>
    public static string New() => Crypto.Base64Url.Encode(
        System.Security.Cryptography.RandomNumberGenerator.GetBytes(TokenByteLength));
}