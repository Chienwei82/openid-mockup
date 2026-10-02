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
/// El puerto lo recibe ya reservado (<paramref name="address"/>) y no lo elige al arrancar: el mock
/// tiene el <c>redirect_uri</c> de este cliente escrito en <c>clients.json</c>, asi que la direccion
/// tiene que existir antes de que arranque cualquiera de los dos.
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
        LoopbackAddress address,
        string issuer,
        string clientId,
        string clientSecret,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder();

        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(CollectingLoggerProvider.Create());
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.WebHost.UseUrls(address.BaseAddress);

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

                // El handler usa response_mode=form_post en el flujo de codigo, y ahi las cookies de
                // correlacion y de nonce viajan en un POST de vuelta desde el mock. Con la politica por
                // defecto serian SameSite=None y Secure, y sobre HTTP plano un navegador las
                // descartaria: el cliente nunca encontraria su correlacion. Un cliente real en
                // desarrollo sobre HTTP ajusta esto mismo; sobre HTTPS no hay que tocar nada.
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.NonceCookie.SameSite = SameSiteMode.Lax;
                options.NonceCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

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
            // ?anterior=1 reenvia el refresh token que la renovacion anterior ya canjeo, para poder
            // observar la rotacion desde fuera de la aplicacion.
            var reusePrevious = context.Request.Query["anterior"] == "1";
            var refreshed = await TokenRefresher.RefreshAsync(context, reusePrevious);

            return refreshed.Succeeded
                ? Results.Json(refreshed.Value!)
                : Results.BadRequest(new { error = refreshed.Error });
        });

        // El logout tiene que cerrar los DOS esquemas: con el de OpenID Connect solo se abandona la
        // sesion del proveedor y la cookie de la aplicacion sigue viva, que es justo el fallo que
        // estas pruebas tienen que detectar.
        application.MapGet("/logout", (HttpContext context) => Results.SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));

        // El handler vuelve aqui tras el end session y despues redirige al RedirectUri final.
        application.MapGet("/signout-callback-oidc", () => Results.Ok());

        // "sesion cerrada" en texto plano: es lo que veria una persona al volver del logout, y la prueba
        // compara la pagina, no un JSON.
        application.MapGet("/", () => Results.Text("sesion cerrada"));
    }

    private const string ProfilePath = "/profile";

    public async ValueTask DisposeAsync()
    {
        await _application.StopAsync();
        await _application.DisposeAsync();
    }
}

/// <summary>
/// Entradas de log del host, para poder ver por que fallo una peticion.
/// </summary>
/// <remarks>
/// Son un almacen global y compartido a proposito: el diagnostico aparece en varios tests y con un
/// almacen por host habia que saber cual. A cambio, el ensamblado no puede paralelizar (ver
/// <c>AssemblyInfo.cs</c>) y el volcado se limita a las entradas posteriores a <see cref="Clear"/>, para
/// que un test no diagnostique con el log de otro.
/// </remarks>
public static class CollectingLoggerProvider
{
    private static readonly List<string> Entries = [];

    public static IReadOnlyList<string> Logged => Entries;

    /// <summary>
    /// Descarta lo collected hasta ahora. Lo llama el arnes al empezar cada test, de modo que
    /// <see cref="Dump"/> solo muestre lo de ese test.
    /// </summary>
    public static void Clear() => Entries.Clear();

    public static string Dump() => string.Join(Environment.NewLine, Entries);

    public static ILoggerProvider Create() => new Provider();

    private sealed class Provider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CollectingLogger(categoryName);

        public void Dispose()
        {
        }
    }

    private sealed class CollectingLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var line = formatter(state, exception);

            lock (Entries)
            {
                Entries.Add($"[{logLevel}] {category}: {line}{(exception is null ? string.Empty : Environment.NewLine + exception)}");
            }
        }
    }
}

/// <summary>Los claims del principal en la forma en que los devuelve el endpoint protegido.</summary>
public static class Claims
{
    public static IReadOnlyDictionary<string, string> Describe(ClaimsPrincipal principal) =>
        principal.Claims.ToDictionary(claim => claim.Type, claim => claim.Value, StringComparer.Ordinal);
}
