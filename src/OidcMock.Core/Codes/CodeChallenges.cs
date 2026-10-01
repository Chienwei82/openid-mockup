using System.Security.Cryptography;
using System.Text;
using OidcMock.Core.Crypto;

namespace OidcMock.Core.Codes;

/// <summary>
/// Calcula y verifica los code challenges de PKCE. La comparacion es en tiempo constante para no
/// filtrar el desafio por temporizacion, aunque el mock sea de desarrollo local.
/// </summary>
public static class CodeChallenges
{
    public static string Create(string method, string codeVerifier) =>
        method switch
        {
            PkceCodeChallengeMethods.Plain => codeVerifier,
            PkceCodeChallengeMethods.Sha256 => Sha256Base64Url(codeVerifier),
            _ => throw new ArgumentException($"Metodo de code challenge no soportado: '{method}'.", nameof(method))
        };

    public static bool Matches(string expectedChallenge, string method, string codeVerifier)
    {
        if (string.IsNullOrEmpty(expectedChallenge) || string.IsNullOrEmpty(codeVerifier))
        {
            return false;
        }

        if (!IsSupported(method))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(expectedChallenge),
            System.Text.Encoding.ASCII.GetBytes(Create(method, codeVerifier)));
    }

    public static bool IsSupported(string method) =>
        string.Equals(method, PkceCodeChallengeMethods.Plain, StringComparison.Ordinal) ||
        string.Equals(method, PkceCodeChallengeMethods.Sha256, StringComparison.Ordinal);

    private static string Sha256Base64Url(string codeVerifier) =>
        Base64Url.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
}