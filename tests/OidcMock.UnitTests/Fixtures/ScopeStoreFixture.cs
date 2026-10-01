using OidcMock.Core.Authorization;
using OidcMock.Core.Scopes;

namespace OidcMock.UnitTests.Fixtures;

/// <summary>
/// ScopeStore en memoria con los scopes de la configuracion de ejemplo, para que los tests de
/// dominio no dependan de leer config/scopes.json del disco.
/// </summary>
public sealed class ScopeStoreFixture
{
    public static IScopeStore Create() =>
        new InMemoryScopeStore(
        [
            new ScopeDefinition("openid", ["sub"]),
            new ScopeDefinition("profile", ["name", "given_name", "family_name"]),
            new ScopeDefinition("email", ["email", "email_verified"]),
            new ScopeDefinition("custom.profile", ["full_name", "login_name"]),
            new ScopeDefinition("roles", ["role"]),
            new ScopeDefinition("nombre", ["nombre"]),
            new ScopeDefinition("documentofva", ["documentofva"]),
            new ScopeDefinition("offline_access", [])
        ]);
}

public sealed class InMemoryScopeStore(IReadOnlyList<ScopeDefinition> scopes) : IScopeStore
{
    public ScopeDefinition? Find(string name) =>
        scopes.FirstOrDefault(scope => string.Equals(scope.Name, name, StringComparison.Ordinal));

    public IReadOnlyList<ScopeDefinition> List() => scopes;
}