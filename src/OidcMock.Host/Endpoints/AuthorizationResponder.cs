using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using OidcMock.Core.Authorization;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Construye la respuesta del endpoint de autorizacion: redireccion con el codigo, o respuesta de
/// error. Respeta el response_mode (query, fragment, form_post) anunciado en el discovery.
/// </summary>
public static class AuthorizationResponder
{
    public static IResult RedirectWithCode(AuthorizationGranted granted, string responseMode, string issuer) =>
        BuildResponse(granted.Code.RedirectUri ?? string.Empty, granted.Code.State, responseMode, issuer, parameters =>
            parameters["code"] = granted.Code.Code);

    /// <summary>
    /// Respuesta del flujo hibrido: el code, el id_token y el session_state salen juntos en el
    /// mismo modo de respuesta, que para este flujo es el fragmento si la peticion no dice otra cosa.
    /// </summary>
    public static IResult RedirectWithHybrid(
        AuthorizationGranted granted,
        string idToken,
        string sessionState,
        string responseMode,
        string issuer) =>
        BuildResponse(granted.Code.RedirectUri ?? string.Empty, granted.Code.State, responseMode, issuer, parameters =>
        {
            parameters["code"] = granted.Code.Code;
            parameters["id_token"] = idToken;
            parameters["session_state"] = sessionState;
        }, includeIssuer: false);

    /// <summary>
    /// Los errores viajan por el redirect_uri solo cuando este ya se valido (CanRedirect). Si el
    /// client_id o el redirect_uri no son validos se responde en el endpoint, para no mandar a la
    /// aplicacion a una URL que el mock no ha verificado.
    /// </summary>
    public static IResult RespondToError(
        AuthorizationValidationResult validation,
        string issuer,
        string responseMode = ResponseModes.Query)
    {
        if (validation.CanRedirect)
        {
            return BuildResponse(validation.RedirectUri!, validation.State, responseMode, issuer, parameters =>
            {
                parameters["error"] = validation.Error!.Code;
                parameters["error_description"] = validation.Error!.Description;
            });
        }

        return Results.Json(
            new { error = validation.Error!.Code, error_description = validation.Error!.Description },
            statusCode: validation.Error!.StatusCode);
    }

    private static IResult BuildResponse(
        string redirectUri,
        string? state,
        string responseMode,
        string issuer,
        Action<Dictionary<string, string>> fill,
        bool includeIssuer = true)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);

        // La respuesta hibrida no lleva iss: el servidor real lo omite aqui pese a anunciarlo en
        // authorization_response_iss_parameter_supported, y el mock imita lo que hace, no lo que
        // dice. El flujo de codigo si lo envia, que es su comportamiento fijado.
        if (includeIssuer)
        {
            parameters["iss"] = issuer;
        }

        fill(parameters);
        AddWhenPresent(parameters, "state", state);

        return responseMode switch
        {
            ResponseModes.Fragment => Results.Redirect(AppendToFragment(redirectUri, parameters)),
            ResponseModes.FormPost => Results.Content(
                FormPostBody.Render(redirectUri, parameters),
                "text/html; charset=utf-8"),
            _ => Results.Redirect(QueryStringHelper.AppendQuery(redirectUri, parameters))
        };
    }

    private static void AddWhenPresent(Dictionary<string, string> parameters, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            parameters[name] = value;
        }
    }

    private static string AppendToFragment(string redirectUri, IReadOnlyDictionary<string, string> parameters) =>
        $"{redirectUri}#{QueryStringHelper.Join(parameters)}";
}

/// <summary>
/// Serializa los parametros de la respuesta de autorizacion en la forma que exige cada response_mode.
/// </summary>
public static class QueryStringHelper
{
    public static string AppendQuery(string redirectUri, IReadOnlyDictionary<string, string> parameters) =>
        $"{redirectUri}?{Join(parameters)}";

    /// <summary>
    /// Los parametros serializados sin el signo de apertura: el query string lo pone AppendQuery y
    /// el fragmento los lleva pegados al #. Un ? de mas dejaria el fragmento como "#?a=b", que no
    /// es lo que emite el servidor real.
    /// </summary>
    public static string Join(IReadOnlyDictionary<string, string> parameters) =>
        QueryHelpers.AddQueryString(
            string.Empty,
            parameters.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .TrimStart('?');
}