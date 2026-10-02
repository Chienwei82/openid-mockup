using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using OidcMock.Core.Configuration;
using OidcMock.Core.Crypto;
using OidcMock.Host.Logging;

namespace OidcMock.Host.Crypto;

/// <summary>
/// Carga la clave de firma desde signing-key.pem y la persiste para que el kid no cambie entre
/// reinicios. Si el archivo no existe genera una RSA 2048 y la escribe.
/// </summary>
public sealed class PemSigningKeyProvider : ISigningKeyProvider, IDisposable
{
    private readonly Lock _gate = new();
    private readonly string _keyFilePath;
    private readonly ILogger _logger;
    private SigningKey? _signingKey;

    public PemSigningKeyProvider(string configDirectory, string keyFileName, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyFileName);

        _keyFilePath = Path.Combine(configDirectory, keyFileName);
        _logger = logger ?? NullLogger.Instance;
    }

    public PemSigningKeyProvider(string configDirectory)
        : this(configDirectory, ConfigurationFiles.SigningKey)
    {
    }

    public PemSigningKeyProvider(string configDirectory, string keyFileName)
        : this(configDirectory, keyFileName, logger: null)
    {
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
        catch (Exception exception) when (IsPermissionProblem(exception))
        {
            // Persistir la clave es una comodidad, no un requisito: sin ella el mock sigue firmando,
            // solo que el kid cambia en cada reinicio y los tokens emitidos antes dejan de validar.
            // Un config montado en solo lectura (Docker como usuario no-root) es un caso normal.
            key.Dispose();
            OidcMockLog.SigningKeyNotPersisted(_logger, _keyFilePath, exception.Message);

            return new SigningKey(RSA.Create(SigningKeySizes.KeySizeInBits));
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    private static bool IsPermissionProblem(Exception exception) =>
        exception is UnauthorizedAccessException or IOException;
}
