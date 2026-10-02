using OidcMock.Core.Authorization;
using OidcMock.Core.Configuration;
using OidcMock.Core.Discovery;
using OidcMock.Core.PushedRequests;
using OidcMock.Core.Users;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Endpoint /connect/authorize. GET valida la peticion y decide que pantalla corresponde segun el
/// prompt y la sesion del navegador; POST procesa el login o el consentimiento de esa pantalla y
/// devuelve el codigo de autorizacion al cliente. La orquestacion vive en AuthorizationFlow.
/// </summary>
public static class AuthorizationEndpoints
{
    public static IEndpointRouteBuilder MapAuthorizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider.GetRequiredService<OidcMockOptions>();
        var group = endpoints.MapGroup(EndpointUri.NormalizePathBase(options.PathBase));

        group.MapGet(EndpointPaths.Authorize, ShowAuthorization);
        group.MapPost(EndpointPaths.Authorize, ProcessDecision);

        return endpoints;
    }

    private static Task<IResult> ShowAuthorization(
        HttpContext context,
        IAuthorizationRequestValidator validator,
        IAuthorizationService authorization,
        IAuthorizationInteraction interaction,
        IUserStore users,
        IAuthSessionStore sessions,
        IPushedAuthorizationService pushedRequests,
        DiscoveryDocumentBuilder discovery,
        OidcMockOptions options) =>
        new AuthorizationFlow(context, validator, authorization, interaction, users, sessions, pushedRequests, discovery, options)
            .ShowAsync();

    private static Task<IResult> ProcessDecision(
        HttpContext context,
        IAuthorizationRequestValidator validator,
        IAuthorizationService authorization,
        IAuthorizationInteraction interaction,
        IUserStore users,
        IAuthSessionStore sessions,
        IPushedAuthorizationService pushedRequests,
        DiscoveryDocumentBuilder discovery,
        OidcMockOptions options) =>
        new AuthorizationFlow(context, validator, authorization, interaction, users, sessions, pushedRequests, discovery, options)
            .ProcessAsync();
}
