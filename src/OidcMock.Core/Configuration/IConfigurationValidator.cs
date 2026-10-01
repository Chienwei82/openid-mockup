namespace OidcMock.Core.Configuration;

/// <summary>
/// Valida la configuracion al arrancar para fallar rapido y no en la primera peticion.
/// </summary>
public interface IConfigurationValidator
{
    void Validate();
}
