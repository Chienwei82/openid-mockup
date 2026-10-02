using OidcMock.Core.Errors;

namespace OidcMock.Core.PushedRequests;

/// <summary>
/// Caso de uso de /connect/par (RFC 9126): guarda la peticion de autorizacion y devuelve un
/// request_uri de un solo uso con el que el cliente invoca despues a /connect/authorize.
/// </summary>
public interface IPushedAuthorizationService
{
    Result<PushedAuthorizationResponse> Push(PushRequestParameters parameters, string issuer);
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