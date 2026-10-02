namespace OidcMock.Core.Errors;

/// <summary>
/// Errores canonicos del mock con su codigo HTTP. Centralizarlos aqui evita que cada endpoint
/// invente su codigo y que el mismo error viaje con status distintos en dos sitios.
/// </summary>
public static class ProtocolErrors
{
    public const int BadRequest = 400;
    public const int Unauthorized = 401;
    public const int Forbidden = 403;
    public const int Success = 200;

    /// <summary>La peticion es invalida o falta un parametro obligatorio (RFC 6749 5.2).</summary>
    public static ProtocolError InvalidRequest(string description) =>
        new("invalid_request", description, BadRequest);

    /// <summary>Fallo la autenticacion del cliente (RFC 6749 5.2). El token endpoint responde 401.</summary>
    public static ProtocolError InvalidClient(string description) =>
        new("invalid_client", description, Unauthorized);

    /// <summary>Codigo, refresh token o credenciales de grant no validos (RFC 6749 5.2).</summary>
    public static ProtocolError InvalidGrant(string description) =>
        new("invalid_grant", description, BadRequest);

    public static ProtocolError UnsupportedGrantType(string description) =>
        new("unsupported_grant_type", description, BadRequest);

    public static ProtocolError InvalidScope(string description) =>
        new("invalid_scope", description, BadRequest);

    public static ProtocolError UnsupportedResponseType(string description) =>
        new("unsupported_response_type", description, BadRequest);

    public static ProtocolError AccessDenied(string description) =>
        new("access_denied", description, BadRequest);

    public static ProtocolError InvalidToken(string description) =>
        new("invalid_token", description, Unauthorized);

    /// <summary>El usuario aun no ha aprobado: el cliente debe seguir sondeando (RFC 8628 3.5).</summary>
    public static ProtocolError AuthorizationPending(string description) =>
        new("authorization_pending", description, BadRequest);

    /// <summary>El codigo de device o de CIBA caduco antes de que el usuario respondiera.</summary>
    public static ProtocolError ExpiredToken(string description) =>
        new("expired_token", description, BadRequest);

    /// <summary>El cliente esta sondeando demasiado rapido y debe espaciar los intentos.</summary>
    public static ProtocolError SlowDown(string description) =>
        new("slow_down", description, BadRequest);

    /// <summary>El cliente no tiene permitido el scope pedido (RFC 6749 5.2, authorization server).</summary>
    public static ProtocolError UnauthorizedClient(string description) =>
        new("unauthorized_client", description, BadRequest);

    public static ProtocolError InvalidRedirectUri(string description) =>
        new("invalid_request", description, BadRequest);

    public static ProtocolError InvalidTarget(string description) =>
        new("invalid_target", description, BadRequest);
}