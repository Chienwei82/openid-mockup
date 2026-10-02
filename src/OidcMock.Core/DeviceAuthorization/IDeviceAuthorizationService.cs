using OidcMock.Core.Clients;
using OidcMock.Core.Errors;

namespace OidcMock.Core.DeviceAuthorization;

/// <summary>
/// Caso de uso de los dos flujos que el cliente inicia asincronamente y luego sondea con un handle:
/// /connect/deviceauthorization (RFC 8628) y /connect/ciba. Los dos comparten lo que de verdad los
/// hace familia: autenticar al cliente, resolver sus scopes y registrar una peticion pendiente con
/// caducidad. La peticion pendiente la consume despues <c>PollGrantHandler</c>.
/// </summary>
public interface IPollAuthorizationService
{
    Result<DeviceAuthorizationResponse> StartDevice(DeviceAuthorizationStart request);

    Result<CibaAuthorizationResponse> StartCiba(CibaAuthorizationStart request);
}

/// <summary>
/// Lo que llega a /connect/deviceauthorization, en la forma en que llega por el cuerpo.
/// </summary>
/// <param name="Credentials">
/// Credenciales del cliente. No se acepta solo el <c>client_id</c>: un cliente confidencial tiene que
/// aportar su secreto, igual que en el token endpoint, PAR, introspect y revocation.
/// </param>
public sealed record DeviceAuthorizationStart(
    ClientCredentials Credentials,
    string? Scope);

/// <summary>Lo que llega a /connect/ciba, en la forma en que llega por el cuerpo.</summary>
/// <param name="WantsUserCode">
/// El cliente declara con <c>user_code_parameter_supported</c> que sabe mostrar el codigo de usuario
/// en su pantalla, y entonces la respuesta lo incluye.
/// </param>
public sealed record CibaAuthorizationStart(
    ClientCredentials Credentials,
    string? LoginHint,
    string? Scope,
    bool WantsUserCode);

/// <summary>
/// Respuesta de device authorization: lo que el dispositivo muestra y sondea.
/// </summary>
public sealed record DeviceAuthorizationResponse(
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    int ExpiresIn,
    int Interval);

/// <summary>Parametros de la peticion de device authorization.</summary>
public sealed record DeviceAuthorizationRequest(
    Core.Clients.Client Client,
    IReadOnlyList<string> Scopes);

/// <summary>
/// Respuesta de CIBA: el <c>auth_req_id</c> con el que el cliente sondea y, si lo pidió, el codigo de
/// usuario con el que su pantalla permite distinguir la peticion.
/// </summary>
public sealed record CibaAuthorizationResponse(
    string AuthReqId,
    int ExpiresIn,
    int Interval,
    string? UserCode = null,
    string? VerificationUri = null);