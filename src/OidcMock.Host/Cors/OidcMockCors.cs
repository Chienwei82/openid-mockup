using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using OidcMock.Core.Configuration;

namespace OidcMock.Host.Cors;

/// <summary>
/// Politica CORS del mock. Solo se aplica a los endpoints que consume un navegador desde otra
/// pagina (discovery, JWKS, token y userinfo); el authorize con su formulario, el introspect y el
/// revocation no se exponen a SPAs, asi que no llevan cabeceras CORS.
/// </summary>
public static class OidcMockCors
{
    /// <summary>Nombre de la politica, para aplicarla endpoint a endpoint.</summary>
    public const string PolicyName = "oidc-mock-browser";

    /// <summary>Metodos que una SPA necesita poder usar contra el mock.</summary>
    private static readonly string[] AllowedMethods = ["GET", "POST", "OPTIONS"];

    /// <summary>
    /// Cabeceras que la SPA puede enviar: Authorization para el Bearer del userinfo y Content-Type
    /// para el formulario del token endpoint. Sin lista explicita, el navegador rechaza el preflight.
    /// </summary>
    private static readonly string[] AllowedHeaders = ["Authorization", "Content-Type"];

    /// <summary>
    /// Cabeceras que el navegador puede leer de la respuesta: las de autenticacion del userinfo
    /// (WWW-Authenticate) y las de cache del token endpoint.
    /// </summary>
    private static readonly string[] ExposedHeaders = ["WWW-Authenticate", "Cache-Control", "Pragma"];

    public static IServiceCollection AddOidcMockCors(this IServiceCollection services)
    {
        services.AddSingleton<ICorsPolicyProvider, OidcMockCorsPolicyProvider>();
        services.AddCors();

        return services;
    }

    /// <summary>
    /// Construye la politica leyendo los origenes de las opciones ya validadas, en vez de fijarlos
    /// al registrar: asi la configuracion manda y los valores por defecto ya se aplicaron.
    /// </summary>
    private sealed class OidcMockCorsPolicyProvider(IOptions<OidcMockOptions> options)
        : ICorsPolicyProvider
    {
        public Task<CorsPolicy?> GetPolicyAsync(HttpContext httpContext, string? policyName)
        {
            if (policyName is null || !string.Equals(policyName, PolicyName, StringComparison.Ordinal))
            {
                return Task.FromResult<CorsPolicy?>(null);
            }

            var origins = options.Value.AllowedCorsOrigins;
            var policy = new CorsPolicy
            {
                // El token endpoint admite credenciales de cliente, y un Allow-Origin: * con
                // credenciales es invalido en los navegadores: se nombra cada origen.
                SupportsCredentials = true
            };

            // Solo los origenes configurados; los demas se quedan sin Allow-Origin y el navegador
            // bloquea la respuesta.
            foreach (var origin in origins)
            {
                policy.Origins.Add(origin);
            }

            foreach (var method in AllowedMethods)
            {
                policy.Methods.Add(method);
            }

            foreach (var header in AllowedHeaders)
            {
                policy.Headers.Add(header);
            }

            foreach (var header in ExposedHeaders)
            {
                policy.ExposedHeaders.Add(header);
            }

            return Task.FromResult<CorsPolicy?>(policy);
        }
    }
}