using System.Collections.Concurrent;
using OidcMock.Core.Configuration;
using OidcMock.Core.Scopes;
using OidcMock.Host.Stores;
using OidcMock.Host.Stores.Configuration;

namespace OidcMock.Host.Stores;

/// <summary>
/// Store de scopes con lectura desde el JSON configurado y escritura en memoria: los cambios
/// hechos vía el endpoint de administracion se perdenden al reiniciar, pero son inmediatos y no
/// exigen restart.
/// </summary>
public sealed class AdminInMemoryScopeStore : IMutableScopeStore
{
    private readonly JsonFileLoader<ScopeFile, IReadOnlyList<ScopeDefinition>> _loader;
    private readonly ConcurrentDictionary<string, ScopeDefinition> _writes = new(StringComparer.Ordinal);

    public AdminInMemoryScopeStore(string configDirectory, bool reloadOnChange)
    {
        _loader = new JsonFileLoader<ScopeFile, IReadOnlyList<ScopeDefinition>>(
            configDirectory,
            ConfigurationFiles.Scopes,
            reloadOnChange,
            ScopeMapper.ToDomain);
    }

    public ScopeDefinition? Find(string name) => _writes.TryGetValue(name, out var scope) ? scope : _loader.Load().FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal));

    public IReadOnlyList<ScopeDefinition> List() => _loader.Load();

    public void Add(ScopeDefinition scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (Find(scope.Name) is not null)
        {
            throw new InvalidOperationException($"El scope '{scope.Name}' ya existe.");
        }
        _writes[scope.Name] = scope;
    }

    public void Update(ScopeDefinition scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (Find(scope.Name) is not null)
        {
            throw new InvalidOperationException($"El scope '{scope.Name}' no existe.");
        }
        _writes[scope.Name] = scope;
    }

    public void Delete(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _writes.TryRemove(name, out _);
    }
}
