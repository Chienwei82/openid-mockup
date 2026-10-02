namespace OidcMock.Core.Clients;

/// <summary>
/// client_secret_basic: el secreto viaja en el encabezado Authorization, que el host ya decodifico
/// y transporto en las credenciales.
/// </summary>
public sealed class ClientSecretBasicAuthenticator : IClientAuthenticator
{
    public string Method => ClientAuthenticationMethods.ClientSecretBasic;

    public Client? Authenticate(Client client, ClientCredentials credentials) =>
        ClientSecrets.Match(client, credentials.ClientSecret) ? client : null;
}