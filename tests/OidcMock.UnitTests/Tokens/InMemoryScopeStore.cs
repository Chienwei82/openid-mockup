using OidcMock.Core.Scopes;

namespace OidcMock.UnitTests.Tokens;

/// <summary>
/// ScopeStore en memoria para las pruebas de emision, que no dependen de la configuracion en disco.
/// </summary>
public sealed class InMemoryScopeStore(IReadOnlyList<ScopeDefinition> scopes) : IScopeStore
{
    public ScopeDefinition? Find(string name) =>
        scopes.FirstOrDefault(scope => string.Equals(scope.Name, name, StringComparison.Ordinal));

    public IReadOnlyList<ScopeDefinition> List() => scopes;
}