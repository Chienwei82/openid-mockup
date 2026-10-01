using System.Security.Cryptography;
using OidcMock.Core.Configuration;
using OidcMock.Core.Crypto;

namespace OidcMock.Host.Crypto;

/// <summary>
/// Carga la clave de firma desde signing-key.pem y la persiste para que el kid no cambie entre
/// reinicios. Si el archivo no existe genera una RSA 2048 y la escribe.
/// </summary>
public sealed class PemSigningKeyProvider : ISigningKeyProvider, IDisposable
{
    private readonly Lock _gate = new();
    private readonly string _keyFilePath;
    private SigningKey? _signingKey;

    public PemSigningKeyProvider(string configDirectory)
        : this(configDirectory, ConfigurationFiles.SigningKey)
    {
    }

    public PemSigningKeyProvider(string configDirectory, string keyFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyFileName);

        _keyFilePath = Path.Combine(configDirectory, keyFileName);
    }

    public SigningKey GetSigningKey()
    {
        lock (_gate)
        {
            return _signingKey ??= LoadOrCreateKey();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _signingKey?.Dispose();
            _signingKey = null;
        }
    }

    private SigningKey LoadOrCreateKey()
    {
        if (!File.Exists(_keyFilePath))
        {
            return GenerateAndPersistKey();
        }

        try
        {
            var key = RSA.Create();
            key.ImportFromPem(File.ReadAllText(_keyFilePath));
            return new SigningKey(key);
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException or IOException)
        {
            throw new ConfigurationException(
                Path.GetFileName(_keyFilePath),
                $"no se pudo leer la clave de firma: {exception.Message}",
                exception);
        }
    }

    private SigningKey GenerateAndPersistKey()
    {
        var key = RSA.Create(SigningKeySizes.KeySizeInBits);

        try
        {
            File.WriteAllText(_keyFilePath, key.ExportPkcs8PrivateKeyPem());
            return new SigningKey(key);
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }
}
