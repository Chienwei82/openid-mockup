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
    string Issuer,
    DateTimeOffset ExpiresAt,
    TimeSpan Interval,
    string? DeviceCode = null,
    string? UserCode = null,
    string? BindingMessage = null)
{
    public bool IsExpiredAt(DateTimeOffset instant) => instant >= ExpiresAt;

    /// <summary>authorization_pending: el usuario todavia no ha aprobado (RFC 8628 3.5).</summary>
    public bool IsAuthorizedAt(DateTimeOffset instant) => Subject is not null && !IsExpiredAt(instant);

    /// <summary>Sujeto que aprobo la peticion, o null si sigue pendiente.</summary>
    public string? Subject { get; init; }

    /// <summary>El usuario denego la solicitud: el sondeo debe responder access_denied.</summary>
    public bool Denied { get; init; }

    public string? UserName { get; init; }

    public DateTimeOffset? AuthenticatedAt { get; init; }
}