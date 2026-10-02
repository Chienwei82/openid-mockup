namespace OidcMock.Core.Clients;

/// <summary>
/// Metodos con los que un cliente se autentica en el token endpoint, tal como los anuncia el
/// discovery. Agregar uno es registrar otra implementacion de <see cref="IClientAuthenticator"/>.
/// </summary>
public static class ClientAuthenticationMethods
{
    public const string ClientSecretBasic = "client_secret_basic";

    public const string ClientSecretPost = "client_secret_post";
}

/// <summary>
/// Como se presenta un cliente en una peticion del token endpoint: el metodo-annunciado, el
/// client_id y el secreto que trajo (que en client_secret_basic viaja en el encabezado
/// Authorization y en client_secret_post en el cuerpo).
/// </summary>
public sealed record ClientCredentials(string Method, string? ClientId, string? ClientSecret);