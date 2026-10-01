using System.Security.Cryptography;
using System.Text.Json;
using OidcMock.Core.Crypto;
using OidcMock.Core.Discovery;

namespace OidcMock.UnitTests.Crypto;

public sealed class JsonWebKeySetBuilderTests
{
    [Fact]
    public void PublicaLaClavePublicaRsaConSuKidYAlg()
    {
        using var key = RSA.Create(SigningKeySizes.KeySizeInBits);

        var publishedKey = Publish(key);

        Assert.Equal("RSA", publishedKey.KeyType);
        Assert.Equal("sig", publishedKey.Use);
        Assert.Equal("RS256", publishedKey.Algorithm);
        Assert.Equal(SigningKeyId.FromPublicKey(key), publishedKey.KeyId);
    }

    [Fact]
    public void ElModuloYElExponenteCoincidenConLaClave()
    {
        using var key = RSA.Create(SigningKeySizes.KeySizeInBits);
        var parameters = key.ExportParameters(includePrivateParameters: false);

        var publishedKey = Publish(key);

        Assert.Equal(parameters.Modulus, DecodeBase64Url(publishedKey.Modulus));
        Assert.Equal(parameters.Exponent, DecodeBase64Url(publishedKey.Exponent));
    }

    [Fact]
    public void ElModuloYElExponenteSePublicanEnBase64Url()
    {
        using var key = RSA.Create(SigningKeySizes.KeySizeInBits);

        var json = JsonSerializer.SerializeToElement(JsonWebKeySetBuilder.Build(key), JsonWebKeySet.SerializerOptions);
        var modulus = json.GetProperty("keys")[0].GetProperty("n").GetString()!;

        Assert.Equal(Convert.ToBase64String(DecodeBase64Url(modulus)).Replace('+', '-').Replace('/', '_').TrimEnd('='), modulus);
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '='));
    }

    [Fact]
    public void ElKidEsEstableParaLaMismaClaveYDistintoParaOtra()
    {
        using var first = RSA.Create(SigningKeySizes.KeySizeInBits);
        using var second = RSA.Create(SigningKeySizes.KeySizeInBits);

        Assert.Equal(SigningKeyId.FromPublicKey(first), SigningKeyId.FromPublicKey(first));
        Assert.NotEqual(SigningKeyId.FromPublicKey(first), SigningKeyId.FromPublicKey(second));
    }

    private static JsonWebKey Publish(RSA key) => JsonWebKeySetBuilder.Build(key).Keys.Single();
}

