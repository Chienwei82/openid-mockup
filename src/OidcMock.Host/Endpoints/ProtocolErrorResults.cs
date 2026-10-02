using OidcMock.Core.Errors;
using OidcMock.Core.Grants;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Cuerpo JSON de error de protocolo, compartido por los endpoints que no son el token endpoint.
/// </summary>
public static class ProtocolErrorResults
{
    public static IResult From(ProtocolError error) =>
        Results.Json(
            new TokenErrorResponse { Error = error.Code, ErrorDescription = error.Description },
            TokenResponse.SerializerOptions,
            statusCode: error.StatusCode);
}