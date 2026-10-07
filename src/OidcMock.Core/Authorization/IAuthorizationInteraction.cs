using OidcMock.Core.Errors;

namespace OidcMock.Core.Authorization;

/// <summary>
/// Que interaccion corresponde a una peticion de autorizacion, segun el prompt y la sesion del
/// navegador. Concentrarlo aqui evita que el endpoint tenga que decidir por su cuenta cuando
/// puede redirigir en silencio y cuando tiene que mostrar algo.
/// </summary>
public enum AuthorizationStep
{
    /// <summary>No hay sesion, o el prompt fuerza el login: hay que pedir credenciales.</summary>
    Login,

    /// <summary>El prompt pide elegir cuenta: hay que mostrar la pantalla de eleccion.</summary>
    SelectAccount,

    /// <summary>Hay sesion pero el prompt pide consentimiento: hay que mostrar la pantalla.</summary>
    Consent,

    /// <summary>Hay sesion y el prompt no pide interaccion: se puede emitir el codigo en silencio.</summary>
    Grant,

    /// <summary>No se puede seguir sin mostrar nada: el error viaja por el redirect_uri.</summary>
    Error
}

/// <summary>Decision del endpoint de autorizacion, con el usuario de la sesion si lo hay.</summary>
public sealed record AuthorizationDecision(AuthorizationStep Step, string? UserName, ProtocolError? Error)
{
    public static AuthorizationDecision NeedsLogin() => new(AuthorizationStep.Login, null, null);

    public static AuthorizationDecision NeedsAccountSelection(string? userName) => new(AuthorizationStep.SelectAccount, userName, null);

    public static AuthorizationDecision NeedsConsent(string userName) => new(AuthorizationStep.Consent, userName, null);

    public static AuthorizationDecision Grants(string userName) => new(AuthorizationStep.Grant, userName, null);

    public static AuthorizationDecision Fails(ProtocolError error) => new(AuthorizationStep.Error, null, error);
}

/// <summary>
/// Decide el paso siguiente del flujo y concede la autorizacion cuando ya hay sesion. El endpoint
/// solo traduce la decision a una pantalla o a una redireccion.
/// </summary>
public interface IAuthorizationInteraction
{
    /// <summary>
    /// Paso que corresponde a la peticion. <<paramref name="sessionId"/> es el identificador de la
    /// cookie de sesion, o null si el navegador no trae ninguna.
    /// </summary>
    AuthorizationDecision Decide(ValidatedAuthorizationRequest authorization, string? sessionId);
}
