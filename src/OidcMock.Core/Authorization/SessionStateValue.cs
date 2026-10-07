using System.Security.Cryptography;
using OidcMock.Core.Crypto;

namespace OidcMock.Core.Authorization;

/// <summary>
/// Genera el session_state de la respuesta de autorizacion en el formato observable del servidor
/// real: 43 caracteres base64url, un punto y 32 hexadecimales en mayuscula. El valor es opaco: al
/// cliente le basta con guardarlo y devolverlo, igual que con el original.
/// </summary>
public static class SessionStateValue
{
    private const int CookieStateByteLength = 32;
    private const int ClientStateByteLength = 16;

    public static string New() =>
        $"{Base64Url.Encode(RandomNumberGenerator.GetBytes(CookieStateByteLength))}." +
        Convert.ToHexString(RandomNumberGenerator.GetBytes(ClientStateByteLength));
}