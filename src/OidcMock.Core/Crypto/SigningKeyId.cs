using System.Security.Cryptography;

namespace OidcMock.Core.Crypto;

/// <summary>
/// Identificador estable de una clave de firma (kid): base64url del SHA-256 de la clave publica
/// en formato SubjectPublicKeyInfo. No cambia entre reinicios mientras la clave persistida no cambie.
/// </summary>
public static class SigningKeyId
{
    public static string FromPublicKey(RSA key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return Base64Url.Encode(SHA256.HashData(key.ExportSubjectPublicKeyInfo()));
    }
}
