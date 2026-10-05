using OidcMock.Core.Authorization;

namespace OidcMock.Core.PendingRequests;

/// <summary>
/// Peticion de autorizacion que el mock guarda "en espera" y que un cliente puede canjear mas tarde
/// con un handle. La comparten los tres flujos que inician de forma asincrona: PAR, Device
/// Authorization y CIBA.
/// </summary>
public sealed record PendingAuthorizationRequest(
    string Handle,
    string ClientId,
    IReadOnlyList<string> Scopes,
    ValidatedAuthorizationRequest Authorization,
    DateTimeOffset ExpiresAt,
    TimeSpan Interval,
    string? DeviceCode = null,
    string? UserCode = null,
    string? BindingMessage = null)
{
    public bool IsExpiredAt(DateTimeOffset instant) => instant >= ExpiresAt;

    /// <summary>authorization_pending: el usuario todavia no ha aprobado (RFC 8628 3.5).</summary>
    public bool IsAuthorizedAt(DateTimeOffset instant) => Subject is not null && !IsExpiredAt(instant);

    /// <summary>
    /// Instante del ultimo sondeo registrado, o null si todavia no se ha sondeado. Lo usa el intervalo
    /// de RFC 8628 3.5: dos sondeos seguidos sin dejar pasar el intervalo es <c>slow_down</c>.
    /// </summary>
    public DateTimeOffset? LastPolledAt { get; init; }

    /// <summary>
    /// Sondeado otra vez sin haber dejado pasar el intervalo. El primer sondeo nunca lo incumple: el
    /// cliente no puede respetar un intervalo que aun no conoce.
    /// </summary>
    public bool PolledTooSoonAt(DateTimeOffset instant) =>
        LastPolledAt is { } last && instant - last < Interval;

    /// <summary>Sujeto que aprobo la peticion, o null si sigue pendiente.</summary>
    public string? Subject { get; init; }

    /// <summary>El usuario denego la solicitud: el sondeo debe responder access_denied.</summary>
    public bool Denied { get; init; }

    public string? UserName { get; init; }

    public DateTimeOffset? AuthenticatedAt { get; init; }
}