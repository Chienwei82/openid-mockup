using OidcMock.Core.Authorization;
using OidcMock.Core.Codes;
using OidcMock.Core.Clients;
using OidcMock.Core.Errors;
using OidcMock.Core.Grants;
using OidcMock.Core.PendingRequests;

namespace OidcMock.Core.PushedRequests;

/// <summary>
/// Empuja la peticion de autorizacion al mock y devuelve un request_uri. El cliente luego llama a
/// /connect/authorize con request_uri y el mock reconstruye la peticion original, sin que el usuario
/// tenga que viajar con todos los parametros en la URL.
/// </summary>
public sealed class PushedAuthorizationService(
    IPendingAuthorizationStore pendingRequests,
    IAuthorizationRequestValidator validator,
    IClientStore clientStore,
    TimeProvider timeProvider) : IPushedAuthorizationService
{
    private static readonly TimeSpan RequestUriLifetime = TimeSpan.FromMinutes(5);

    public Result<PushedAuthorizationResponse> Push(PushRequestParameters parameters, string issuer)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (!Authenticates(parameters, clientStore))
        {
            return Result<PushedAuthorizationResponse>.Fail(
                ProtocolErrors.InvalidClient("Las credenciales del cliente no son validas."));
        }

        var validation = validator.Validate(ToAuthorizationRequest(parameters));
        if (validation.IsError)
        {
            return Result<PushedAuthorizationResponse>.Fail(validation.Error!);
        }

        var issuedAt = timeProvider.GetUtcNow();
        var handle = OpaqueToken.New();

        pendingRequests.Issue(new PendingAuthorizationRequest(
            handle,
            validation.Value!.Client.ClientId,
            validation.Value.Scopes,
            validation.Value,
            issuer,
            issuedAt + RequestUriLifetime,
            TimeSpan.Zero));

        return Result<PushedAuthorizationResponse>.Ok(
            new PushedAuthorizationResponse(handle, (int)RequestUriLifetime.TotalSeconds));
    }

    /// <summary>
    /// PAR autentica al cliente igual que el token endpoint. Un cliente publico (sin secreto) es
    /// valido: la proteccion la aporta el PKCE ya registrado.
    /// </summary>
    private static bool Authenticates(PushRequestParameters parameters, IClientStore clientStore)
    {
        if (string.IsNullOrEmpty(parameters.ClientId))
        {
            return false;
        }

        var client = clientStore.Find(parameters.ClientId);

        return client is not null &&
            (!client.RequireClientSecret ||
                string.Equals(client.ClientSecret, parameters.ClientSecret, StringComparison.Ordinal));
    }

    private static AuthorizationRequest ToAuthorizationRequest(PushRequestParameters parameters) =>
        new(
            parameters.ClientId,
            parameters.RedirectUri,
            parameters.ResponseType,
            SplitScopes(parameters.Scope),
            parameters.Nonce,
            parameters.State,
            parameters.CodeChallenge,
            parameters.CodeChallengeMethod,
            parameters.Prompt,
            GrantTypes.AuthorizationCode,
            parameters.ResponseMode ?? ResponseModes.Query);

    private static string[] SplitScopes(string? scope) =>
        string.IsNullOrWhiteSpace(scope)
            ? []
            : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}