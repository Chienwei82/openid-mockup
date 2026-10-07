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

        var options = endpoints.ServiceProvider.GetRequiredService<OidcMockOptions>();
        var pathBase = EndpointUri.NormalizePathBase(options.PathBase);

        endpoints.MapGet(Root, ShowClients);
        endpoints.MapGet(AuthorizeUrlRoute, ShowAuthorizeUrl);

        if (pathBase != Root)
        {
            var group = endpoints.MapGroup(pathBase);
            group.MapGet(Root, ShowClients);
            group.MapGet(AuthorizeUrlRoute, ShowAuthorizeUrl);
        }

        return endpoints;
    }

    private static IResult ShowClients(IClientStore clients, OidcMockOptions options) =>
        Results.Content(
            HomePage.Render(clients.List(), EndpointUri.NormalizePathBase(options.PathBase)),
            HtmlContentType);

    private static IResult ShowAuthorizeUrl(
        HttpRequest request,
        IClientStore clients,
        DiscoveryDocumentBuilder discovery,
        OidcMockOptions options)
    {
        var pathBase = EndpointUri.NormalizePathBase(options.PathBase);
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
