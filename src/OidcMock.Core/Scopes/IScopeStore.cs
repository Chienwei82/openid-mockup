namespace OidcMock.Core.Scopes;

/// <summary>
/// Repositorio de solo lectura de scopes soportados.
/// </summary>
public interface IScopeStore
{
    ScopeDefinition? Find(string name);

    IReadOnlyList<ScopeDefinition> List();
}
