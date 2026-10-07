using System.Text.Json.Serialization;
using OidcMock.Core.DeviceAuthorization;
using OidcMock.Core.Discovery;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Endpoints de autorizacion por sondeo: device authorization, CIBA y el iframe de check_session.
/// </summary>
public static class PollEndpoints
{
    public static IEndpointRouteBuilder MapPollEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost(EndpointPaths.DeviceAuthorization, StartDeviceAuthorization).DoNotStore();
        endpoints.MapPost(EndpointPaths.Ciba, StartCiba).DoNotStore();
        endpoints.MapGet(
            EndpointPaths.CheckSession,
            () => Results.Content(CheckSessionPage.Body, "text/html; charset=utf-8"));

        return endpoints;
    }

    private static async Task<IResult> StartDeviceAuthorization(
        HttpContext context,
        IPollAuthorizationService pollAuthorization)
    {
        var values = await RequestValues.ReadAsync(context.Request);

        var result = pollAuthorization.StartDevice(new DeviceAuthorizationStart(
            ClientCredentialsReader.Read(context.Request, values),
            values.GetValueOrDefault("scope")));

        return result.Failed
            ? ProtocolErrorResults.From(result.Error!)
            : Results.Json(new DeviceAuthorizationBody(
                result.Value!.DeviceCode,
                result.Value.UserCode,
                result.Value.VerificationUri,
                result.Value.ExpiresIn,
                result.Value.Interval));
    }

    /// <summary>
    /// CIBA: el cliente inicia la autorizacion desde su backend y recibe un <c>auth_req_id</c> con el
    /// que sondea. La pantalla de aprobacion no la tiene el mock, asi que el resultado sale del propio
    /// endpoint: la logica de CIBA vive en <see cref="IPollAuthorizationService"/>.
    /// </summary>
    private static async Task<IResult> StartCiba(
        HttpContext context,
        IPollAuthorizationService pollAuthorization)
    {
        var values = await RequestValues.ReadAsync(context.Request);

        var result = pollAuthorization.StartCiba(new CibaAuthorizationStart(
            ClientCredentialsReader.Read(context.Request, values),
            values.GetValueOrDefault("login_hint"),
            values.GetValueOrDefault("scope"),
            WantsUserCode(values)));

        return result.Failed
            ? ProtocolErrorResults.From(result.Error!)
            : Results.Json(new CibaResponse(
                result.Value!.AuthReqId,
                result.Value.ExpiresIn,
                result.Value.Interval,
                result.Value.UserCode,
                result.Value.VerificationUri));
    }

    /// <summary>
    /// El codigo de usuario es opcional en CIBA: solo se devuelve si el cliente declara que lo admite
    /// con <c>user_code_parameter_supported</c>, como hacen los que lo muestran en su pantalla para
    /// que el usuario pueda distinguir la peticion.
    /// </summary>
    private static bool WantsUserCode(IReadOnlyDictionary<string, string> values) =>
        string.Equals(
            values.GetValueOrDefault("user_code_parameter_supported"),
            "true",
            StringComparison.OrdinalIgnoreCase);
}

/// <summary>Cuerpo de la respuesta de device authorization (RFC 8628 3.2).</summary>
public sealed record DeviceAuthorizationBody(
    [property: JsonPropertyName("device_code")] string DeviceCode,
    [property: JsonPropertyName("user_code")] string UserCode,
    [property: JsonPropertyName("verification_uri")] string VerificationUri,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("interval")] int Interval);

/// <summary>
/// Cuerpo de la respuesta de CIBA. Los dos ultimos campos solo se rellenan cuando el cliente ha
/// pedido el codigo de usuario, y por eso son opcionales en la forma serializada.
/// </summary>
public sealed record CibaResponse(
    [property: JsonPropertyName("auth_req_id")] string AuthReqId,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("interval")] int Interval,
    [property: JsonPropertyName("user_code")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? UserCode = null,
    [property: JsonPropertyName("verification_uri")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? VerificationUri = null);
