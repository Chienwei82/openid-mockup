using OidcMock.Core.Configuration;
using OidcMock.Core.Crypto;
using OidcMock.Core.Discovery;
using OidcMock.Core.Scopes;
using OidcMock.Host.Cors;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Endpoints de metadatos del mock: discovery y JWKS, bajo el PathBase configurado.
/// </summary>
public static class DiscoveryEndpoints
{
    public static IEndpointRouteBuilder MapDiscoveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider.GetRequiredService<OidcMockOptions>();
        var group = endpoints.MapGroup(EndpointUri.NormalizePathBase(options.PathBase))
            .RequireCors(OidcMockCors.PolicyName);

        group.MapGet(EndpointPaths.Configuration, PublishDiscovery);
        group.MapGet(EndpointPaths.Jwks, PublishJsonWebKeySet);

        return endpoints;
    }

    private static IResult PublishDiscovery(
        HttpContext context,
        DiscoveryDocumentBuilder builder,
        OidcMockOptions options)
    {
        var issuer = ResolveIssuer(builder, options, context.Request);
        return Results.Json(builder.Build(issuer), DiscoveryDocument.SerializerOptions);
    }

    private static IResult PublishJsonWebKeySet(ISigningKeyProvider signingKeyProvider)
    {
        using var signingKey = signingKeyProvider.GetSigningKey();
        return Results.Json(JsonWebKeySetBuilder.Build(signingKey.Key), JsonWebKeySet.SerializerOptions);
    }

    private static string ResolveIssuer(DiscoveryDocumentBuilder builder, OidcMockOptions options, HttpRequest request) =>
        builder.ResolveIssuer(request.Scheme, request.Host.Value ?? string.Empty);
}
