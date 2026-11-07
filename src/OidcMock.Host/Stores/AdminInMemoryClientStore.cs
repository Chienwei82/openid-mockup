using System.Collections.Concurrent;
using OidcMock.Core.Clients;
using OidcMock.Core.Configuration;
using OidcMock.Host.Stores;
using OidcMock.Host.Stores.Configuration;

namespace OidcMock.Host.Stores;

/// <summary>
/// Store de clientes con lectura desde el JSON configurado y escritura en memoria: los
/// cambios hechos vía el endpoint de administracion se perdenden al reiniciar, pero son inmediatos
/// y no exigen restart. El decorador mantiene la misma regla de lectura del JSON (de lo
/// contrario, una lista vaga de la memoria se serviria si el JSON se carga despues).
/// </summary>
public sealed class AdminInMemoryClientStore : IMutableClientStore
{
    private readonly JsonFileLoader<ClientFile, IReadOnlyList<Client>> _loader;
    private readonly ConcurrentDictionary<string, Client> _writes = new(StringComparer.Ordinal);

    public AdminInMemoryClientStore(string configDirectory, bool reloadOnChange)
    {
        _loader = new JsonFileLoader<ClientFile, IReadOnlyList<Client>>(
            configDirectory,
            ConfigurationFiles.Clients,
            reloadOnChange,
            ClientMapper.ToDomain);
    }

    public Client? Find(string clientId)
    {
        return _writes.TryGetValue(clientId, out var client) ? client : _loader.Load().FirstOrDefault(c => string.Equals(c.ClientId, clientId, StringComparison.Ordinal));
    }

    public IReadOnlyList<Client> List() => _loader.Load();

    public void Add(Client client)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (Find(client.ClientId) is not null)
        {
            throw new InvalidOperationException($"El cliente '{client.ClientId}' ya existe.");
        }
        _writes[client.ClientId] = client;
    }

    public void Update(Client client)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (Find(client.ClientId) is not null)
        {
            throw new InvalidOperationException($"El cliente '{client.ClientId}' no existe.");
        }
        _writes[client.ClientId] = client;
    }

    public void Delete(string clientId)
    {
        ArgumentException.ThrowIfNullOrEmpty(clientId);
        _writes.TryRemove(clientId, out _);
    }
}
