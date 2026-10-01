using System.Security.Cryptography;
using OidcMock.Core.Crypto;

namespace OidcMock.Core.Discovery;

/// <summary>
/// Construye el JWKS a partir de la clave RSA de firma del mock.
/// </summary>
public static class JsonWebKeySetBuilder
{
    private const string RsaKeyType = "RSA";
    private const string SignatureUse = "sig";
    private const string SigningAlgorithm = "RS256";

    public static JsonWebKeySet Build(RSA key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return new JsonWebKeySet { Keys = [BuildKey(key)] };
    }

    private static JsonWebKey BuildKey(RSA key)
    {
        var parameters = key.ExportParameters(includePrivateParameters: false);

        return new JsonWebKey
        {
            KeyType = RsaKeyType,
            Use = SignatureUse,
            KeyId = SigningKeyId.FromPublicKey(key),
            Algorithm = SigningAlgorithm,
            Modulus = Base64Url.Encode(parameters.Modulus!),
            Exponent = Base64Url.Encode(parameters.Exponent!)
        };
    }
}
