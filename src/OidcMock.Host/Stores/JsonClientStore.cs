using OidcMock.Core.Clients;
using OidcMock.Core.Configuration;
using OidcMock.Host.Stores.Configuration;

namespace OidcMock.Host.Stores;

/// <summary>
/// Store de clientes respaldado por clients.json.
/// </summary>
public sealed class JsonClientStore : IClientStore
{
    private readonly JsonFileLoader<ClientFile> _loader;

    public JsonClientStore(string configDirectory)
        : this(configDirectory, reloadOnChange: true)
    {
    }

    public JsonClientStore(string configDirectory, bool reloadOnChange)
    {
        _loader = new JsonFileLoader<ClientFile>(configDirectory, ConfigurationFiles.Clients, reloadOnChange);
    }

    public Client? Find(string clientId) =>
        Clients().FirstOrDefault(client => string.Equals(client.ClientId, clientId, StringComparison.Ordinal));

    public IReadOnlyList<Client> List() => Clients();

    private IReadOnlyList<Client> Clients() => ClientMapper.ToDomain(_loader.Load());
}
