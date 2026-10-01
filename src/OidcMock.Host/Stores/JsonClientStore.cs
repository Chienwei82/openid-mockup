using OidcMock.Core.Clients;
using OidcMock.Core.Configuration;
using OidcMock.Host.Stores.Configuration;

namespace OidcMock.Host.Stores;

/// <summary>
/// Store de clientes respaldado por clients.json.
/// </summary>
public sealed class JsonClientStore : IClientStore
{
    private readonly JsonFileLoader<ClientFile, IReadOnlyList<Client>> _loader;

    public JsonClientStore(string configDirectory)
        : this(configDirectory, reloadOnChange: true)
    {
    }

    public JsonClientStore(string configDirectory, bool reloadOnChange)
    {
        _loader = new JsonFileLoader<ClientFile, IReadOnlyList<Client>>(
            configDirectory,
            ConfigurationFiles.Clients,
            reloadOnChange,
            ClientMapper.ToDomain);
    }

    public Client? Find(string clientId) =>
        _loader.Load().FirstOrDefault(client => string.Equals(client.ClientId, clientId, StringComparison.Ordinal));

    public IReadOnlyList<Client> List() => _loader.Load();
}
