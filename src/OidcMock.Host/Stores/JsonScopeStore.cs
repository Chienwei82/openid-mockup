using OidcMock.Core.Configuration;
using OidcMock.Core.Scopes;
using OidcMock.Host.Stores.Configuration;

namespace OidcMock.Host.Stores;

/// <summary>
/// Store de scopes respaldado por scopes.json.
/// </summary>
public sealed class JsonScopeStore : IScopeStore
{
    private readonly JsonFileLoader<ScopeFile, IReadOnlyList<ScopeDefinition>> _loader;

    public JsonScopeStore(string configDirectory)
        : this(configDirectory, reloadOnChange: true)
    {
    }

    public JsonScopeStore(string configDirectory, bool reloadOnChange)
    {
        _loader = new JsonFileLoader<ScopeFile, IReadOnlyList<ScopeDefinition>>(
            configDirectory,
            ConfigurationFiles.Scopes,
            reloadOnChange,
            ScopeMapper.ToDomain);
    }

    public ScopeDefinition? Find(string name) =>
        _loader.Load().FirstOrDefault(scope => string.Equals(scope.Name, name, StringComparison.Ordinal));

    public IReadOnlyList<ScopeDefinition> List() => _loader.Load();
}
