using OidcMock.Core.Crypto;

namespace OidcMock.Core.Codes;

/// <summary>
/// Genera el code_verifier de PKCE (RFC 7636 4.1): un valor aleatorio de 256 bits en base64url. Su
/// pareja es el code_challenge que viaja en el authorize; el verifier se guarda para el canje.
/// </summary>
public static class CodeVerifier
{
    private const int VerifierByteLength = 32;

    public static string New() => Base64Url.Encode(
        System.Security.Cryptography.RandomNumberGenerator.GetBytes(VerifierByteLength));
}
