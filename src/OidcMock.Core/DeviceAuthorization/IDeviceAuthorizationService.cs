using OidcMock.Core.Errors;
using OidcMock.Core.PendingRequests;

namespace OidcMock.Core.DeviceAuthorization;

/// <summary>
/// Caso de uso de /connect/deviceauthorization (RFC 8628): emite un device_code y un user_code, y
/// registra la peticion como pendiente para que el usuario la apruebe en el navegador.
/// </summary>
public interface IDeviceAuthorizationService
{
    Result<DeviceAuthorizationResponse> Start(DeviceAuthorizationRequest request);
}

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