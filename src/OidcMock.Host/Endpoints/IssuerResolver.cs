using OidcMock.Core.Configuration;
using OidcMock.Core.Discovery;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Resuelve el issuer de una peticion, igual que hace el discovery, para que el codigo, el id_token
/// y los errores salgan con el mismo emisor que anuncia el documento.
/// </summary>
public static class IssuerResolver
{
    public static string Resolve(HttpRequest request, DiscoveryDocumentBuilder builder, OidcMockOptions options) =>
        options.Issuer is null
            ? builder.ResolveIssuer(request.Scheme, request.Host.Value ?? string.Empty)
            : EndpointUri.NormalizeIssuer(options.Issuer);
}