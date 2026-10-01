using OidcMock.Core.Configuration;
using OidcMock.Core.Scopes;
using OidcMock.Host.Stores.Configuration;

namespace OidcMock.Host.Stores;

/// <summary>
/// Store de scopes respaldado por scopes.json.
/// </summary>
public sealed class JsonScopeStore : IScopeStore
{
    private readonly JsonFileLoader<ScopeFile> _loader;

    public JsonScopeStore(string configDirectory)
        : this(configDirectory, reloadOnChange: true)
    {
    }

    public JsonScopeStore(string configDirectory, bool reloadOnChange)
    {
        _loader = new JsonFileLoader<ScopeFile>(configDirectory, ConfigurationFiles.Scopes, reloadOnChange);
    }

    public ScopeDefinition? Find(string name) =>
        Scopes().FirstOrDefault(scope => string.Equals(scope.Name, name, StringComparison.Ordinal));

    public IReadOnlyList<ScopeDefinition> List() => Scopes();

    private IReadOnlyList<ScopeDefinition> Scopes() => ScopeMapper.ToDomain(_loader.Load());
}
