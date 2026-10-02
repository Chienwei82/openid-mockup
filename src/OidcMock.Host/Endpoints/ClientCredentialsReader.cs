using OidcMock.Core.Clients;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Lee las credenciales del cliente de una peticion: el encabezado Authorization gana al cuerpo
/// (RFC 6749 2.3.1), igual que en el token endpoint, para que un endpoint no tenga su propia idea de
/// como se presenta un cliente.
/// </summary>
public static class ClientCredentialsReader
{
    public static ClientCredentials Read(HttpRequest request, IReadOnlyDictionary<string, string> values) =>
        BasicAuthorizationHeader.TryRead(request, out var fromHeader)
            ? new ClientCredentials(
                ClientAuthenticationMethods.ClientSecretBasic,
                fromHeader.ClientId,
                fromHeader.Secret)
            : new ClientCredentials(
                ClientAuthenticationMethods.ClientSecretPost,
                values.GetValueOrDefault("client_id"),
                values.GetValueOrDefault("client_secret"));
}
