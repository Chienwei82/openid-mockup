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
        builder.ResolveIssuer(request.Scheme, request.Host.Value ?? string.Empty, PathBase(request, options));

    /// <summary>
    /// Prefijo bajo el que se sirve esta peticion: el PathBase real de la peticion (que en IIS pone el
    /// propio IIS y, en Kestrel, fija UsePathBase) y, si viene vacio, el configurado. Es el que se usa
    /// para las URLs que el mock pinta (el logo) y para el path de la cookie de sesion.
    /// </summary>
    public static string PathBase(HttpRequest request, OidcMockOptions options) =>
        request.PathBase.HasValue
            ? request.PathBase.Value!
            : EndpointUri.NormalizePathBase(options.EffectivePathBase);
}