using OidcMock.Core.Authorization;
using OidcMock.Core.Errors;

namespace OidcMock.Core.PushedRequests;

/// <summary>
/// Caso de uso de /connect/par (RFC 9126): guarda la peticion de autorizacion y devuelve un
/// request_uri de un solo uso con el que el cliente invoca despues a /connect/authorize.
/// </summary>
public interface IPushedAuthorizationService
{
    Result<PushedAuthorizationResponse> Push(PushRequestParameters parameters, string issuer);

    /// <summary>
    /// Reconstruye la peticion original desde un request_uri. La resuelve el authorize, porque un
    /// cliente queAnnuncia PAR llega al authorize con solo el <c>client_id</c> y esta referencia.
    /// </summary>
    Result<AuthorizationRequest> Find(string requestUri);

    /// <summary>
    /// Invalida el request_uri. Lo hace el mock al emitir el codigo: RFC 9126 4 lo declara de un solo
    /// uso, y reutilizarlo permitiria generar mas de un codigo con una sola peticion empujada.
    /// </summary>
    void Consume(string requestUri);
}

/// <summary>
/// Respuesta de PAR: el request_uri y lo que el cliente necesita para el canje posterior.
/// </summary>
public sealed record PushedAuthorizationResponse(
    string RequestUri,
    int ExpiresIn);

/// <summary>
/// Parametros de la peticion de autorizacion en su forma de texto plano, tal como llega en PAR.
/// </summary>
public sealed record PushRequestParameters(
    string? ClientId,
    string? ClientSecret,
    string? RedirectUri,
    string? ResponseType,
    string? Scope,
    string? State,
    string? Nonce,
    string? CodeChallenge,
    string? CodeChallengeMethod,
    string? ResponseMode,
    string? Prompt);