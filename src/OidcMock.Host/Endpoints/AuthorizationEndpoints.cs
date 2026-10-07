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

        endpoints.MapGet(EndpointPaths.Authorize, ShowAuthorization);
        endpoints.MapPost(EndpointPaths.Authorize, ProcessDecision);

        // El servidor real expone el authorize tambien como /connect/authorize/callback: es el
        // destino del ReturnUrl de su pantalla de login y por donde entra la prueba.
        endpoints.MapGet(EndpointPaths.AuthorizeCallback, ShowAuthorization);
        endpoints.MapPost(EndpointPaths.AuthorizeCallback, ProcessDecision);

        return endpoints;
    }

    private static Task<IResult> ShowAuthorization(
        HttpContext context,
        AuthorizationBinder binder,
        IAuthorizationService authorization,
        IAuthorizationInteraction interaction,
        IConsentStore consents,
        IUserStore users,
        IUserOverlay overlay,
        IAuthSessionStore sessions,
        Core.Tokens.ITokenFactory tokens,
        DiscoveryDocumentBuilder discovery,
        OidcMockOptions options) =>
        new AuthorizationFlow(context, binder, authorization, interaction, consents, users, overlay, sessions, tokens, discovery, options)
            .ShowAsync();

    private static Task<IResult> ProcessDecision(
        HttpContext context,
        AuthorizationBinder binder,
        IAuthorizationService authorization,
        IAuthorizationInteraction interaction,
        IConsentStore consents,
        IUserStore users,
        IUserOverlay overlay,
        IAuthSessionStore sessions,
        Core.Tokens.ITokenFactory tokens,
        DiscoveryDocumentBuilder discovery,
        OidcMockOptions options) =>
        new AuthorizationFlow(context, binder, authorization, interaction, consents, users, overlay, sessions, tokens, discovery, options)
            .ProcessAsync();
}
