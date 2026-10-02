using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace OidcMock.ClientCompatibilityTests.Infrastructure;

/// <summary>
/// Segunda aplicacion de la prueba: un cliente OpenID Connect de verdad, configurado **solo** con el
/// issuer del mock. No recibe ni la ruta del discovery, ni el token endpoint, ni el JWKS: los
/// descubre por metadata, igual que haria una aplicacion en desarrollo.
///
/// Se configura con las opciones por defecto del handler en todo lo que el prompt no pide cambiar:
/// response type <c>code</c>, PKCE, <c>GetClaimsFromUserInfoEndpoint</c> y guardado de tokens. Si el
/// mock se apartara del protocolo, estos tests lo detectan; si el test los configurara a medida,
/// estarian probando el test.
///
/// Se levanta sobre Kestrel en un puerto reservado porque el navegador de las pruebas (un
/// <see cref="HttpClient"/> con <c>AllowAutoRedirect=false</c> y un <c>CookieContainer</c>) recorre
/// las redirecciones a mano, igual que un navegador, y necesita una URL real.
/// </summary>
public sealed class OidcClientHost : IAsyncDisposable
{
    private readonly WebApplication _application;

    private OidcClientHost(WebApplication application)
    {
        _application = application;
    }

    public string BaseAddress { get; private init; } = string.Empty;

    /// <summary>Servicios del cliente, para leer sus opciones ya resueltas en las pruebas.</summary>
    public IServiceProvider Services => _application.Services;

    public static async Task<OidcClientHost> StartAsync(
        string issuer,
        string clientId,
        string clientSecret,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder();

        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.UseUrls($"http://127.0.0.1:{LoopbackAddress.Reserve().Port}");

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie()
            .AddOpenIdConnect(options =>
            {
                options.Authority = issuer;
                options.ClientId = clientId;
                options.ClientSecret = clientSecret;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;

                // El mock sirve HTTP plano en loopback: RequireHttpsMetadata solo obliga cuando la
                // metadata es HTTPS, y no hay TLS que configurar en una prueba de protocolo.
                options.RequireHttpsMetadata = false;

                options.Scope.Clear();
                foreach (var scope in scopes)
                {
                    options.Scope.Add(scope);
                }
            });

        var application = builder.Build();
        MapEndpoints(application);

        await application.StartAsync(cancellationToken);

        var host = new OidcClientHost(application)
        {
            BaseAddress = application.Urls.First()
        };

        return host;
    }

    /// <summary>
    /// Rutas minimas de una aplicacion que usa OpenID Connect: entrar, un recurso protegido que
    /// devuelve los claims del principal, renovar la sesion y salir. <c>/refresh</c> es donde se
    /// ejercita la renovacion: el handler OIDC no renueva solo, lo hace la aplicacion con el
    /// refresh token que el propio handler guardo en la cookie.
    /// </summary>
    private static void MapEndpoints(WebApplication application)
    {
        application.MapGet("/login", (HttpContext context) => Results.Challenge(
            new AuthenticationProperties { RedirectUri = ProfilePath },
            [OpenIdConnectDefaults.AuthenticationScheme]));

        application.MapGet(ProfilePath, (HttpContext context) =>
            context.User.Identity?.IsAuthenticated == true
                ? Results.Json(Claims.Describe(context.User))
                : Results.Unauthorized());

        application.MapGet("/refresh", async (HttpContext context) =>
        {
            var refreshed = await TokenRefresher.RefreshAsync(context);

            return refreshed.Succeeded
                ? Results.Json(refreshed.Value!)
                : Results.BadRequest(new { error = refreshed.Error });
        });

        application.MapGet("/logout", (HttpContext context) => Results.SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            [OpenIdConnectDefaults.AuthenticationScheme]));

        // El handler vuelve aqui tras el end session y despues redirige al RedirectUri final.
        application.MapGet("/signout-callback-oidc", () => Results.Ok());

        application.MapGet("/", () => Results.Ok("sesion cerrada"));
    }

    private const string ProfilePath = "/profile";

    public async ValueTask DisposeAsync()
    {
        await _application.StopAsync();
        await _application.DisposeAsync();
    }
}

/// <summary>Los claims del principal en la forma en que los devuelve el endpoint protegido.</summary>
public static class Claims
{
    public static IReadOnlyDictionary<string, string> Describe(ClaimsPrincipal principal) =>
        principal.Claims.ToDictionary(claim => claim.Type, claim => claim.Value, StringComparer.Ordinal);
}