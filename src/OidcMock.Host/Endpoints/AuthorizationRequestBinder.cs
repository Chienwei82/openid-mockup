using OidcMock.Core.Authorization;
using OidcMock.Core.Grants;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Convierte los parametros de la peticion HTTP en un AuthorizationRequest, leyendo del query string
/// en el GET inicial y del formulario en el POST del login. Los dos caminos deben producir la misma
/// peticion, asi que comparten esta lectura.
/// </summary>
public static class AuthorizationRequestBinder
{
    public static async Task<AuthorizationRequest> BindAsync(HttpRequest request)
    {
        var values = await RequestValues.ReadAsync(request);

        return new AuthorizationRequest(
            values.GetValueOrDefault("client_id"),
            values.GetValueOrDefault("redirect_uri"),
            values.GetValueOrDefault("response_type"),
            SplitScopes(values.GetValueOrDefault("scope")),
            values.GetValueOrDefault("nonce"),
            values.GetValueOrDefault("state"),
            values.GetValueOrDefault("code_challenge"),
            values.GetValueOrDefault("code_challenge_method"),
            values.GetValueOrDefault("prompt"),
            values.GetValueOrDefault("grant_type") ?? GrantTypes.AuthorizationCode,
            values.GetValueOrDefault("response_mode") ?? ResponseModes.Query,
            values.GetValueOrDefault(RequestUriField));
    }

    /// <summary>Nombre del parametro con la peticion empujada (RFC 9126 2).</summary>
    public const string RequestUriField = "request_uri";

    private static string[] SplitScopes(string? scope) =>
        string.IsNullOrWhiteSpace(scope)
            ? []
            : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

}