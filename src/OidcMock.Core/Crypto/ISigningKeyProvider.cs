namespace OidcMock.Core.Crypto;

/// <summary>
/// Provee la clave RSA con la que el mock firma sus tokens. La implementacion la persiste para
/// que el kid y la validacion sobrevivan a un reinicio.
/// </summary>
public interface ISigningKeyProvider
{
    SigningKey GetSigningKey();
}
