using System.Security.Cryptography;
using System.Text;
using OidcMock.Core.Crypto;

namespace OidcMock.Core.Tokens;

/// <summary>
/// Calcula at_hash y c_hash (OpenID Connect Core 3.1.3.6): base64url de la mitad izquierda del
/// hash del valor, con el mismo algoritmo de firma del token (SHA-256 para RS256).
/// </summary>
public static class TokenHash
{
    public static string FromAccessToken(string accessToken) => Compute(accessToken);

    public static string FromAuthorizationCode(string authorizationCode) => Compute(authorizationCode);

    private static string Compute(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var digest = SHA256.HashData(Encoding.ASCII.GetBytes(value));

        return Base64Url.Encode(digest.AsSpan(0, digest.Length / 2));
    }
}