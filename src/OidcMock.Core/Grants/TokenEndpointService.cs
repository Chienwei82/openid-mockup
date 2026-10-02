using OidcMock.Core.Clients;
using OidcMock.Core.Errors;
using OidcMock.Core.Scopes;

namespace OidcMock.Core.Grants;

/// <summary>
/// Caso de uso del token endpoint. Primero autentica al cliente y valida los scopes, y solo despues
/// invoca el handler del grant. Cada comprobacion devuelve un error de protocolo y sale temprano,
/// de modo que el dispatcher no conoce los detalles de ningun grant.
/// </summary>
public sealed class TokenEndpointService(
    IClientStore clientStore,
    IScopeStore scopeStore,
    GrantHandlerRegistry handlers) : ITokenEndpointService
{
    public Result<TokenResponse> IssueToken(TokenEndpointRequest request, string issuer)
    {
        ArgumentNullException.ThrowIfNull(request);

        var client = AuthenticateClient(request);
        if (client is null)
        {
            return Result<TokenResponse>.Fail(
                ProtocolErrors.InvalidClient("Las credenciales del cliente no son validas."));
        }

        if (!client.AllowsGrantType(request.GrantType ?? string.Empty))
        {
            return Result<TokenResponse>.Fail(
                ProtocolErrors.UnsupportedGrantType(
                    $"El grant_type '{request.GrantType}' no esta permitido para este cliente."));
        }

        var handler = handlers.Find(request.GrantType ?? string.Empty);
        if (handler is null)
        {
            return Result<TokenResponse>.Fail(
                ProtocolErrors.UnsupportedGrantType($"El grant_type '{request.GrantType}' no esta soportado."));
        }

        var scopes = ResolveScopes(client, request.Scopes, IsKnownScope);
        if (scopes is null)
        {
            return Result<TokenResponse>.Fail(
                ProtocolErrors.InvalidScope("Uno de los scopes solicitados no esta permitido para el cliente."));
        }

        return handler.Handle(new TokenRequest(
            client,
            issuer,
            scopes,
            request.Code,
            request.RedirectUri,
            request.CodeVerifier,
            request.RefreshToken,
            request.UserName,
            request.Password,
            request.DeviceCode));
    }

    /// <summary>
    /// Cliente publico (sin secreto) es valido: en authorization_code el secreto no viaja y la
    /// proteccion la da el PKCE, que exige el authorize endpoint.
    /// </summary>
    private Client? AuthenticateClient(TokenEndpointRequest request)
    {
        if (string.IsNullOrEmpty(request.ClientId))
        {
            return null;
        }

        var client = clientStore.Find(request.ClientId);
        if (client is null || !client.RequireClientSecret)
        {
            return client;
        }

        return SecretsMatch(client, request.ClientSecret) ? client : null;
    }

    private static bool SecretsMatch(Client client, string? presentedSecret) =>
        !string.IsNullOrEmpty(client.ClientSecret) &&
        !string.IsNullOrEmpty(presentedSecret) &&
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(client.ClientSecret),
            System.Text.Encoding.UTF8.GetBytes(presentedSecret));

    private static IReadOnlyList<string>? ResolveScopes(
        Client client,
        IReadOnlyList<string> requestedScopes,
        Func<string, bool> isKnownScope)
    {
        if (requestedScopes.Count == 0)
        {
            return [ScopeNames.OpenId];
        }

        return requestedScopes.All(scope => client.AllowsScope(scope) && isKnownScope(scope))
            ? requestedScopes
            : null;
    }

    private bool IsKnownScope(string scope) => scopeStore.Find(scope) is not null;
}