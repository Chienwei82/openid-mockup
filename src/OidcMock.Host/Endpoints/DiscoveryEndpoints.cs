using OidcMock.Core.Configuration;
using OidcMock.Core.Crypto;
using OidcMock.Core.Discovery;
using OidcMock.Core.Scopes;
using OidcMock.Host.Cors;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Endpoints de metadatos del mock: discovery y JWKS. Se registran en la raiz: el prefijo de rutas lo
/// aporta <c>UsePathBase</c> (o el propio IIS cuando el mock va en una ruta relativa).
/// </summary>
public static class DiscoveryEndpoints
{
    public static IEndpointRouteBuilder MapDiscoveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(EndpointPaths.Configuration, PublishDiscovery).RequireCors(OidcMockCors.PolicyName);
        endpoints.MapGet(EndpointPaths.Jwks, PublishJsonWebKeySet).RequireCors(OidcMockCors.PolicyName);

        return endpoints;
    }

    private static IResult PublishDiscovery(
        HttpContext context,
        DiscoveryDocumentBuilder builder,
        OidcMockOptions options) =>
        Results.Json(
            builder.Build(IssuerResolver.Resolve(context.Request, builder, options)),
            DiscoveryDocument.SerializerOptions);

    private static IResult PublishJsonWebKeySet(ISigningKeyProvider signingKeyProvider)
    {
        // La clave la posee y dispose el proveedor: liberarla aqui tumbaria la firma de los tokens
        // que se emitieran despues, porque es la misma instancia cacheada.
        var signingKey = signingKeyProvider.GetSigningKey();
        return Results.Json(JsonWebKeySetBuilder.Build(signingKey.Key), JsonWebKeySet.SerializerOptions);
    }
}
