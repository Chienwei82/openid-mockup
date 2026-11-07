namespace OidcMock.Core.Scopes;

/// <summary>
/// Store de scopes con operaciones de escritura, para el endpoint de administración.
/// </summary>
public interface IMutableScopeStore : IScopeStore
{
    void Add(ScopeDefinition scope);
    void Update(ScopeDefinition scope);
    void Delete(string name);
}