namespace OidcMock.Core.Clients;

/// <summary>client_secret_post: el secreto viaja en el cuerpo de la peticion.</summary>
public sealed class ClientSecretPostAuthenticator : IClientAuthenticator
{
    public string Method => ClientAuthenticationMethods.ClientSecretPost;

    public Client? Authenticate(Client client, ClientCredentials credentials) =>
        ClientSecrets.Match(client, credentials.ClientSecret) ? client : null;
}