namespace OidcMock.Core.Clients;

/// <summary>
/// Store de clientes con operaciones de escritura, para el endpoint de administración.
/// </summary>
public interface IMutableClientStore : IClientStore
{
    void Add(Client client);
    void Update(Client client);
    void Delete(string clientId);
}