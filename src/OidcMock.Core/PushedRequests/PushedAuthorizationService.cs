using OidcMock.Core.Authorization;
using OidcMock.Core.Codes;
using OidcMock.Core.Clients;
using OidcMock.Core.Errors;
using OidcMock.Core.Grants;
using OidcMock.Core.PendingRequests;
using OidcMock.Core.Scopes;

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

    public Result<PushedAuthorizationResponse> Push(PushRequestParameters parameters)
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
            ScopeNames.Split(parameters.Scope),
            parameters.Nonce,
            parameters.State,
            parameters.CodeChallenge,
            parameters.CodeChallengeMethod,
            parameters.Prompt,
            GrantTypes.AuthorizationCode,
            parameters.ResponseMode ?? ResponseModes.Query);

    /// <summary>
    /// Reconstruye la peticion original. Se devuelve como <see cref="AuthorizationRequest"/> y no como
    /// la forma ya validada para que el authorize la vuelva a pasar por sus reglas: el codigo emitido
    /// sale de la validacion de hoy, no de la que hizo el push.
    /// </summary>
    public Result<AuthorizationRequest> Find(string requestUri)
    {
        if (string.IsNullOrEmpty(requestUri))
        {
            return Result<AuthorizationRequest>.Fail(
                AuthorizationErrors.InvalidRequestUri(UnknownRequestUri));
        }

        var pending = pendingRequests.Find(requestUri);

        return pending.Failed
            ? Result<AuthorizationRequest>.Fail(pending.Error!)
            : Result<AuthorizationRequest>.Ok(ToRequest(pending.Value!.Authorization, requestUri));
    }

    /// <inheritdoc />
    public void Consume(string requestUri) => pendingRequests.Invalidate(requestUri);

    private static AuthorizationRequest ToRequest(ValidatedAuthorizationRequest authorization, string requestUri) =>
        new(
            authorization.Client.ClientId,
            authorization.RedirectUri,
            authorization.ResponseType,
            authorization.Scopes,
            authorization.Nonce,
            authorization.State,
            authorization.CodeChallenge,
            authorization.CodeChallengeMethod,
            authorization.Prompt,
            GrantTypes.AuthorizationCode,
            authorization.ResponseMode,
            requestUri);

    private const string UnknownRequestUri =
        "El request_uri no corresponde a ninguna peticion empujada vigente del mock.";
}