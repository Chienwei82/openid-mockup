using OidcMock.Core.Clients;

namespace OidcMock.UnitTests.Fixtures;

/// <summary>
/// ClienteStore en memoria con clientes de ejemplo, para que los tests de dominio no dependan de
/// leer config/clients.json del disco.
/// </summary>
public sealed class ClientStoreFixture
{
    public const string SpaClientId = "web-app-spa";
    public const string ServiceClientId = "backend-service";

    public static IClientStore Create() => new InMemoryClientStore([Spa(), Service()]);

    /// <summary>Coordinador con los dos metodos de autenticacion que el token endpoint admite.</summary>
    public static ClientAuthenticator AuthenticatorOver(IClientStore clientStore) =>
        new(clientStore, [new ClientSecretBasicAuthenticator(), new ClientSecretPostAuthenticator()]);

    public static Client Spa() => new(
        SpaClientId,
        ClientSecret: null,
        ["https://localhost:5173/callback", "http://localhost:5173/callback"],
        ["http://localhost:5173/"],
        ["authorization_code", "refresh_token"],
        ["openid", "profile", "email", "custom.profile", "roles", "offline_access"],
        RequirePkce: true,
        RequireClientSecret: false,
        new TokenLifetimes(
            AccessToken: TimeSpan.FromMinutes(30),
            IdentityToken: TimeSpan.FromMinutes(30),
            RefreshToken: TimeSpan.FromHours(8),
            AuthorizationCode: TimeSpan.FromMinutes(5)),
        new Branding("OidcMock - Persona Física", "/assets/logo-mock.svg", "#00695C"));

    public static Client Service() => new(
        ServiceClientId,
        "super-secret-backend",
        [],
        [],
        ["client_credentials", "password", "refresh_token"],
        ["openid", "custom.profile", "roles", "email"],
        RequirePkce: false,
        RequireClientSecret: true,
        new TokenLifetimes(
            AccessToken: TimeSpan.FromHours(1),
            IdentityToken: TimeSpan.FromMinutes(30),
            RefreshToken: TimeSpan.FromHours(12),
            AuthorizationCode: TimeSpan.FromMinutes(5)),
        new Branding("OidcMock - Servicio Backend", "/assets/logo-service.svg", "#1565C0"));
}

public sealed class InMemoryClientStore(IReadOnlyList<Client> clients) : IClientStore
{
    public Client? Find(string clientId) =>
        clients.FirstOrDefault(client => string.Equals(client.ClientId, clientId, StringComparison.Ordinal));

    public IReadOnlyList<Client> List() => clients;
}