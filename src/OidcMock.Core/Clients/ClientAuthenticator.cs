namespace OidcMock.Core.Clients;

/// <summary>
/// Resuelve el cliente de la peticion y delega la comprobacion del secreto en la estrategia del
/// metodo que se uso. Cliente publico (require_client_secret=false) se acepta solo por client_id:
/// en authorization_code el secreto no viaja y la proteccion la da el PKCE.
/// </summary>
public sealed class ClientAuthenticator(IClientStore clientStore, IEnumerable<IClientAuthenticator> authenticators)
{
    private readonly IClientAuthenticator[] _authenticators =
        authenticators?.ToArray() ?? throw new ArgumentNullException(nameof(authenticators));

    private readonly IClientStore _clientStore = clientStore ?? throw new ArgumentNullException(nameof(clientStore));

    /// <summary>Metodos que el mock admite, para que el discovery no los anuncie por su cuenta.</summary>
    public IReadOnlyList<string> SupportedMethods => [.. _authenticators.Select(authenticator => authenticator.Method)];

    public Client? Authenticate(ClientCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        if (string.IsNullOrEmpty(credentials.ClientId))
        {
            return null;
        }

        var client = _clientStore.Find(credentials.ClientId);
        if (client is null || !client.RequireClientSecret)
        {
            return client;
        }

        var authenticator = Find(credentials.Method);

        return authenticator?.Authenticate(client, credentials);
    }

    private IClientAuthenticator? Find(string method) =>
        _authenticators.FirstOrDefault(
            authenticator => string.Equals(authenticator.Method, method, StringComparison.Ordinal));
}