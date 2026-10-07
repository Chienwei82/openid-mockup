using OidcMock.Core.Authorization;
using OidcMock.Core.Clients;
using OidcMock.Core.Configuration;
using OidcMock.Core.Discovery;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Pantalla raiz del mock: en vez de rechazar la conexion, lista los clientes configurados y genera
/// la URL de authorize de un clic. Se sirve en la raiz del host y tambien bajo el PathBase, para que
/// funcione en cualquier despliegue.
/// </summary>
public static class HomeEndpoints
{
    private const string Root = "/";
    private const string AuthorizeUrlRoute = "/authorize-url";
    private const string ClientField = "client";
    private const string HtmlContentType = "text/html; charset=utf-8";

    public static IEndpointRouteBuilder MapHomeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(Root, ShowClients);
        endpoints.MapGet(AuthorizeUrlRoute, ShowAuthorizeUrl);

        return endpoints;
    }

    private static IResult ShowClients(HttpRequest request, IClientStore clients, OidcMockOptions options) =>
        Results.Content(
            HomePage.Render(clients.List(), IssuerResolver.PathBase(request, options)),
            HtmlContentType);

    private static IResult ShowAuthorizeUrl(
        HttpRequest request,
        IClientStore clients,
        DiscoveryDocumentBuilder discovery,
        OidcMockOptions options)
    {
        var pathBase = IssuerResolver.PathBase(request, options);
        var clientId = request.Query[ClientField].FirstOrDefault();

        if (string.IsNullOrEmpty(clientId))
        {
            return Results.Content(HomePage.Render(clients.List(), pathBase), HtmlContentType);
        }

        var client = clients.Find(clientId);

        if (client is null)
        {
            return Results.NotFound();
        }

        return client.RedirectUris.Count == 0
            ? Results.Content(AuthorizeUrlPage.RenderUnsupported(client, pathBase), HtmlContentType)
            : Results.Content(
                AuthorizeUrlPage.Render(client, AuthorizeUrlComposer.Compose(IssuerResolver.Resolve(request, discovery, options), client), pathBase),
                HtmlContentType);
    }
}
