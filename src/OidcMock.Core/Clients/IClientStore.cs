namespace OidcMock.Core.Clients;

/// <summary>
/// Repositorio de solo lectura de clientes registrados.
/// </summary>
public interface IClientStore
{
    Client? Find(string clientId);

    IReadOnlyList<Client> List();
}
