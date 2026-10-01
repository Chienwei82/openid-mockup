using OidcMock.Core.Codes;

namespace OidcMock.Core.Authorization;

/// <summary>
/// Resultado de una autorizacion concedida: el codigo emitido y los datos que hay que devolver al
/// cliente en el redirect_uri.
/// </summary>
public sealed record AuthorizationGranted(AuthorizationCode Code)
{
    public IReadOnlyDictionary<string, string> ToParameters(string issuer) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["code"] = Code.Code,
            ["iss"] = issuer,
            ["state"] = Code.State ?? string.Empty
        };
}

/// <summary>
/// Credenciales que el usuario escribe en la pantalla de login del mock.
/// </summary>
public sealed record SignInRequest(
    string UserName,
    string Password,
    ValidatedAuthorizationRequest Authorization,
    string ResponseMode);