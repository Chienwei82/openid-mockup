using System.Security.Cryptography;
using OidcMock.Core.Crypto;

namespace OidcMock.Core.Tokens;

/// <summary>
/// Genera el identificador unico jti de un token (RFC 7519), aleatorio y en base64url.
/// </summary>
public static class TokenId
{
    private const int IdByteLength = 16;

    public static string New() => Base64Url.Encode(RandomNumberGenerator.GetBytes(IdByteLength));
}