using OidcMock.Core.Configuration;
using OidcMock.Core.Discovery;
using OidcMock.Core.PushedRequests;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Endpoint /connect/par (RFC 9126): el cliente empuja la peticion de autorizacion y recibe un
/// request_uri de un solo uso.
/// </summary>
public static class PushedRequestEndpoints
{
    public static IEndpointRouteBuilder MapPushedRequestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider.GetRequiredService<OidcMockOptions>();
        var group = endpoints.MapGroup(EndpointUri.NormalizePathBase(options.PathBase));

        group.MapPost(EndpointPaths.PushedAuthorizationRequest, Push).DoNotStore();

        return endpoints;
    }

    private static async Task<IResult> Push(HttpContext context, IPushedAuthorizationService par)
    {
        var values = await RequestValues.ReadAsync(context.Request);

        var result = par.Push(ToPushParameters(context.Request, values));

        return result.Succeeded
            ? Results.Json(new
            {
                request_uri = result.Value!.RequestUri,
                expires_in = result.Value.ExpiresIn
            })
            : ProtocolErrorResults.From(result.Error!);
    }

    /// Las credenciales se leen con el lector comun, que da prioridad al encabezado Authorization
    /// (RFC 6749 2.3.1): PAR tiene que aceptar los mismos metodos que el token endpoint, y no solo el
    /// <c>client_secret</c> del cuerpo.
    /// </summary>
    private static PushRequestParameters ToPushParameters(
        HttpRequest request,
        IReadOnlyDictionary<string, string> values) =>
        new(
            ClientCredentialsReader.Read(request, values),
            values.GetValueOrDefault("redirect_uri"),
            values.GetValueOrDefault("response_type"),
            values.GetValueOrDefault("scope"),
            values.GetValueOrDefault("state"),
            values.GetValueOrDefault("nonce"),
            values.GetValueOrDefault("code_challenge"),
            values.GetValueOrDefault("code_challenge_method"),
            values.GetValueOrDefault("response_mode"),
            values.GetValueOrDefault("prompt"));
}