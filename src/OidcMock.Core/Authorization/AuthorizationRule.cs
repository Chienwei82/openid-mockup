using OidcMock.Core.Authorization;
using OidcMock.Core.Clients;
using OidcMock.Core.Codes;
using OidcMock.Core.Errors;
using OidcMock.Core.Grants;
using OidcMock.Core.Scopes;

namespace OidcMock.Core.Authorization;

/// <summary>
/// Una sola regla de la cadena de validacion de /connect/authorize. Devuelve el error que impide
/// continuar, o null si la peticion cumple la regla. Al ser reglas independientes y en orden,
/// anadir una comprobacion es registrar otra implementacion, sin condicionales anidados.
/// </summary>
public interface IAuthorizeRequestValidator
{
    /// <summary>
    /// Si el error que devuelve esta regla puede viajar por el redirect_uri del cliente. Solo las
    /// reglas que comprueban el client_id y el redirect_uri devuelven false: antes de validar
    /// esos dos no se conoce una URL verificada, y redirigir a ella seria un open redirect.
    /// </summary>
    bool ErrorIsRedirectable { get; }

    ProtocolError? Validate(AuthorizeValidationContext context);
}

/// <summary>
/// Peticion a validar junto con el cliente resuelto, que es null cuando el client_id no existe.
/// </summary>
public sealed record AuthorizeValidationContext(AuthorizationRequest Request, Client? Client);

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