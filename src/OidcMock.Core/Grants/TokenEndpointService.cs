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
    IScopeStore scopeStore,
    ClientAuthenticator clientAuthenticator,
    GrantHandlerRegistry handlers) : ITokenEndpointService
{
    public async Task<Result<TokenResponse>> IssueTokenAsync(TokenEndpointRequest request, string issuer)
    {
        ArgumentNullException.ThrowIfNull(request);

        var client = clientAuthenticator.Authenticate(request.Credentials);
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

        // Unica construccion posicional de TokenRequest que sobrevive, y es a proposito: el servicio
        // mapea los once campos uno a uno, que es justo lo que fijan los tests de TokenRequestMapping.
        // En cualquier otro sitio el constructor posicional es un riesgo (D-044) y se usan las factorias.
        return await handler.HandleAsync(new TokenRequest(
            client,
            issuer,
            scopes,
            request.Code,
            request.RedirectUri,
            request.CodeVerifier,
            request.RefreshToken,
            request.UserName,
            request.Password,
            request.DeviceCode,
            request.Scopes.Count > 0));
    }

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