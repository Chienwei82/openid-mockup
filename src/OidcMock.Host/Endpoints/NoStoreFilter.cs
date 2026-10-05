using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Marca la respuesta de un endpoint que lleva una credencial o datos de usuario para que no se
/// guarde en ninguna cache.
///
/// RFC 6749 5.1 lo exige para el token endpoint, y el resto de endpoints del mock devuelve material
/// igual de sensible: el <c>device_code</c> y el <c>user_code</c>, el <c>request_uri</c> de un solo uso,
/// el estado de un token de terceros en introspection y los claims de una persona en userinfo. Sin
/// <c>no-store</c>, un proxy o el navegador los guardarian.
///
/// Se aplica como filtro y no escribiendo la cabecera a mano en cada handler, porque asi el endpoint
/// no puede olvidarse: se declara una vez al mapear.
/// </summary>
public sealed class NoStoreFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store, no-cache";
        context.HttpContext.Response.Headers.Pragma = "no-cache";

        return await next(context);
    }
}

public static class NoStoreEndpointExtensions
{
    /// <summary>
    /// Aplica <see cref="NoStoreFilter"/> a un endpoint. Toda respuesta del endpoint, incluidas las de
    /// error, lleva la cabecera: un 401 con el secreto equivocado tampoco debe quedar cacheado.
    /// </summary>
    public static TBuilder DoNotStore<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(new NoStoreFilter());

        return builder;
    }
}
