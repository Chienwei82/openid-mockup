using OidcMock.Core.Clients;
using OidcMock.Core.Codes;
using OidcMock.Core.Errors;
using OidcMock.Core.Scopes;

namespace OidcMock.Core.Authorization;

/// <summary>
/// Valida la peticion de autorizacion contra la configuracion del mock, dejando el resultado listo
/// para responder por redirect_uri o mostrarlo en el endpoint.
/// </summary>
public interface IAuthorizationRequestValidator
{
    AuthorizationValidationResult Validate(AuthorizationRequest request);
}

/// <summary>
/// Una regla de validacion de la peticion de autorizacion: devuelve el error que impide continuar, o
/// null si la peticion cumple la regla. Permite encadenar reglas sin condicionales anidados.
/// </summary>
public delegate ProtocolError? AuthorizationRule(Client client, AuthorizationRequest request);