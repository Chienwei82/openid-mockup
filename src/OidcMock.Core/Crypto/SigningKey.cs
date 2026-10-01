using System.Security.Cryptography;

namespace OidcMock.Core.Crypto;

/// <summary>
/// Clave de firma en memoria, con su identificador estable (kid).
/// </summary>
public sealed class SigningKey : IDisposable
{
    public SigningKey(RSA key)
    {
        ArgumentNullException.ThrowIfNull(key);

        Key = key;
    }

    public RSA Key { get; }

    public string KeyId => SigningKeyId.FromPublicKey(Key);

    public int KeySize => Key.KeySize;

    public void Dispose() => Key.Dispose();
}
