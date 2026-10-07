namespace OidcMock.Host.Endpoints;

/// <summary>
/// Sirve los logos del branding desde recursos embebidos en el binario. Embebidos y no un wwwroot:
/// el publish es de un solo archivo y los archivos sueltos acaban en el directorio de extraccion o
/// no acaban. Solo existen los recursos embebidos, que es la lista blanca: lo que no esta en
/// Assets/ devuelve 404. Se registra una sola vez en la raiz: el prefijo (p. ej. /oidc) lo aporta
/// UsePathBase o el propio IIS, y las pantallas pintan el logo ya con ese prefijo.
/// </summary>
public static class AssetEndpoints
{
    private const string Route = "/assets/{fileName}";
    private const string ResourcePrefix = "OidcMock.Host.Assets.";
    private const string SvgContentType = "image/svg+xml";

    public static IEndpointRouteBuilder MapAssetEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(Route, Serve);

        return endpoints;
    }

    private static IResult Serve(string fileName)
    {
        var resource = typeof(AssetEndpoints).Assembly.GetManifestResourceStream(ResourcePrefix + fileName);

        return resource is null
            ? Results.NotFound()
            : Results.Stream(resource, SvgContentType);
    }
}