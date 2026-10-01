using OidcMock.Core.Clients;
using OidcMock.Core.Configuration;
using OidcMock.Core.Scopes;
using OidcMock.Core.Users;

namespace OidcMock.Host.Stores;

/// <summary>
/// Fuerza la lectura de los tres archivos de configuracion al arrancar (fail fast).
/// </summary>
internal sealed class JsonConfigurationValidator(
    IClientStore clientStore,
    IUserStore userStore,
    IScopeStore scopeStore) : IConfigurationValidator
{
    public void Validate()
    {
        clientStore.List();
        userStore.List();
        scopeStore.List();
    }
}
