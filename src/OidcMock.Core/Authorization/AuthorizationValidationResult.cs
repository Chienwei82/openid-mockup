using OidcMock.Core.Errors;

namespace OidcMock.Core.Authorization;

/// <summary>
/// Resultado de validar la peticion de autorizacion. Distingue los errores que se devuelven al
/// cliente redirigiendo a su redirect_uri de los que se muestran directamente en el endpoint: solo
/// se redirige cuando el client_id y el redirect_uri ya son validos, para no convertir el endpoint
/// de autorizacion en un vector de open redirect.
/// </summary>
public sealed record AuthorizationValidationResult
{
    private AuthorizationValidationResult(
        ValidatedAuthorizationRequest? request,
        ProtocolError? error,
        string? redirectUri,
        string? state)
    {
        Value = request;
        Error = error;
        RedirectUri = redirectUri;
        State = state;
    }

    public ValidatedAuthorizationRequest? Value { get; }

    public ProtocolError? Error { get; }

    public string? RedirectUri { get; }

    public string? State { get; }

    public bool Succeeded => Value is not null;

    public bool IsError => Value is null;

    public bool CanRedirect => Error is not null && !string.IsNullOrEmpty(RedirectUri);

    public static AuthorizationValidationResult Ok(ValidatedAuthorizationRequest request) =>
        new(request, null, null, null);

    public static AuthorizationValidationResult Failed(ProtocolError error, string? redirectUri, string? state) =>
        new(null, error, redirectUri, state);

    public static AuthorizationValidationResult Failed(ProtocolError error) => new(null, error, null, null);
}