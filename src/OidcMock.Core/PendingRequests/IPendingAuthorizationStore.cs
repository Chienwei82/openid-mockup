using OidcMock.Core.Errors;

namespace OidcMock.Core.PendingRequests;

/// <summary>
/// Almacena las peticiones de autorizacion pendientes de canjear: PAR, device code y CIBA. Todas
/// comparten el mismo ciclo de vida (expiran, se canjean una vez y se sondean con un intervalo).
/// </summary>
public interface IPendingAuthorizationStore
{
    PendingAuthorizationRequest Issue(PendingAuthorizationRequest request);

    /// <summary>
    /// Consulta sin consumir: la usa el sondeo (poll) para responder authorization_pending mientras el
    /// usuario no ha decidido, y para canjear cuando si lo ha hecho.
    /// </summary>
    Result<PendingAuthorizationRequest> Find(string handle);

    /// <summary>Consulta y consume la peticion cuando ya esta autorizada.</summary>
    Result<PendingAuthorizationRequest> Redeem(string handle);

    /// <summary>Marca la peticion como autorizada, como haria el usuario al aprobar en la pantalla.</summary>
    Result<PendingAuthorizationRequest> Approve(
        string handle,
        string userName,
        string subject,
        DateTimeOffset authenticatedAt);

    Result<PendingAuthorizationRequest> Deny(string handle);

    /// <summary>
    /// Invalida la peticion sin mas. Lo usa PAR: su <c>request_uri</c> no se canjea por el sondeo, sino
    /// al emitir el codigo de autorizacion, y <see cref="Redeem"/> solo borra lo ya aprobado.
    /// </summary>
    void Invalidate(string handle);

    void Expire();

    IReadOnlyList<PendingAuthorizationRequest> List();
}