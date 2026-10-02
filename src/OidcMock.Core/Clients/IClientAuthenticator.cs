namespace OidcMock.Core.Clients;

/// <summary>
/// Estrategia de autenticacion de cliente. Una implementacion por metodo, y el coordinador
/// <see cref="ClientAuthenticator"/> elige la que corresponde al metodo que declaro la peticion.
/// </summary>
public interface IClientAuthenticator
{
    /// <summary>Metodo que implementa, tal como viaja en el discovery.</summary>
    string Method { get; }

    /// <summary>
    /// Cliente al que pertenece la peticion, o null si el secreto no corresponde. Resolver el
    /// cliente por client_id es responsabilidad del coordinador, no de cada metodo.
    /// </summary>
    Client? Authenticate(Client client, ClientCredentials credentials);
}