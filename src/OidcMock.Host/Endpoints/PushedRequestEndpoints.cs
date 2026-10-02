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

        group.MapPost(EndpointPaths.PushedAuthorizationRequest, Push);

        return endpoints;
    }

    private static async Task<IResult> Push(HttpContext context, IPushedAuthorizationService par)
    {
        var result = par.Push(ToPushParameters(await RequestValues.ReadAsync(context.Request)));

        return result.Succeeded
            ? Results.Json(new
            {
                request_uri = result.Value!.RequestUri,
                expires_in = result.Value.ExpiresIn
            })
            : ProtocolErrorResults.From(result.Error!);
    }

    private static PushRequestParameters ToPushParameters(IReadOnlyDictionary<string, string> values) =>
        new(
            values.GetValueOrDefault("client_id"),
            values.GetValueOrDefault("client_secret"),
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