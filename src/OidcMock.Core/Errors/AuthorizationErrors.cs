namespace OidcMock.Core.Errors;

/// <summary>
/// Errores propios del endpoint de autorizacion (OpenID Connect Core 3.1.2.6). Alli el error viaja
/// por el redirect_uri como parametro, no como respuesta JSON del endpoint.
/// </summary>
public static class AuthorizationErrors
{
    /// <summary>
    /// El cliente no existe. En el endpoint de autorizacion se responde 400, no 401 como en el token
    /// endpoint (RFC 6749 5.2 aplica a la autenticacion de cliente del token endpoint).
    /// </summary>
    public static ProtocolError InvalidClient(string description) =>
        new("invalid_client", description, ProtocolErrors.BadRequest);

    public static ProtocolError AccessDenied(string description) =>
        new("access_denied", description, ProtocolErrors.BadRequest);

    public static ProtocolError InteractionRequired(string description) =>
        new("interaction_required", description, ProtocolErrors.BadRequest);

    public static ProtocolError LoginRequired(string description) =>
        new("login_required", description, ProtocolErrors.BadRequest);

    public static ProtocolError ConsentRequired(string description) =>
        new("consent_required", description, ProtocolErrors.BadRequest);

    public static ProtocolError InvalidRequestUri(string description) =>
        new("invalid_request_uri", description, ProtocolErrors.BadRequest);

    public static ProtocolError InvalidRequestObject(string description) =>
        new("invalid_request_object", description, ProtocolErrors.BadRequest);

    public static ProtocolError RequestNotSupported(string description) =>
        new("request_not_supported", description, ProtocolErrors.BadRequest);

    public static ProtocolError RequestUriNotSupported(string description) =>
        new("request_uri_not_supported", description, ProtocolErrors.BadRequest);

    public static ProtocolError RegistrationNotSupported(string description) =>
        new("registration_not_supported", description, ProtocolErrors.BadRequest);
}